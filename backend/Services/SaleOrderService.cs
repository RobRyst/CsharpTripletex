using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using backend.Infrastructure.Data;
using backend.Mappers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http.Headers;

namespace backend.Services
{
    public class SaleOrderService : ISaleOrderService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly ILogger<SaleOrderService> _logger;
        public SaleOrderService(AppDbContext context, HttpClient httpClient, ITokenService tokenService, ILogger<SaleOrderService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task SyncSaleOrdersFromTripletexAsync()
        {
            try
            {
                var orders = await GetSaleOrdersFromTripletexAsync();

                _logger.LogInformation("Retrieved {Count} orders from Tripletex", orders.Count);

                foreach (var dto in orders)
                {
                    var existing = await _context.Saleorders.FirstOrDefaultAsync(o => o.TripletexId == dto.Id);

                    if (existing == null)
                    {
                        var order = SaleOrderMapper.ToEntity(dto);
                        _context.Saleorders.Add(order);
                        _logger.LogInformation("New order added: {OrderNumber}", dto.Number);
                    }
                    else
                    {
                        SaleOrderMapper.UpdateEntity(existing, dto);
                        _logger.LogInformation("Existing order updated: {OrderNumber}", dto.Number);
                    }
                }

                _logger.LogInformation("Saving changes to database");
                await _context.SaveChangesAsync();
                await Task.Delay(2000);
                _logger.LogInformation("Save successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during order sync");
                throw;
            }
        }

        public async Task<List<SaleOrderDto>> GetSaleOrdersFromTripletexAsync()
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();
            var fromDate = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd");
            var toDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            var url = $"https://api-test.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}&fields=id,number,status,orderDate,customer(id,name,email)";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "YourApp/1.0");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error getting sale orders: {StatusCode} - {Error}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed getting sale order: {response.StatusCode} - {responseContent}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            try
            {
                SaleOrderListResponse result;
                try
                {
                    result = JsonSerializer.Deserialize<SaleOrderListResponse>(responseContent, options);
                }
                catch (JsonException)
                {
                    var alt = JsonSerializer.Deserialize<SaleOrderListResponseAlternative>(responseContent, options);
                    result = new SaleOrderListResponse { Value = alt?.Values ?? new() };
                }

                return result?.Value ?? new();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing JSON from Tripletex");
                throw;
            }
        }

        public async Task<List<SaleOrderDto>> GetAllWithUserAsync()
        {
            return await _context.Saleorders
                .Include(o => o.Customer)
                .Select(o => new SaleOrderDto
                {
                    Id = o.Id,
                    Number = o.Number,
                    Status = o.Status,
                    OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue),
                    Amount = o.Amount,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.Name
                })
                .ToListAsync();
        }

        public async Task<SaleOrderDto?> GetSaleOrderByIdAsync(int id)
        {
            var order = await _context.Saleorders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new KeyNotFoundException($"Sale order with ID {id} not found.");

            return new SaleOrderDto
            {
                Id = order.Id,
                Number = order.Number,
                Status = order.Status,
                OrderDate = order.OrderDate.ToDateTime(TimeOnly.MinValue),
                Amount = order.Amount,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.Name
            };
        }
public async Task<SaleOrderDto> CreateSaleOrderAsync(TripletexCreateSaleOrder dto)
{
    _logger.LogInformation("Received DTO with CustomerId: {CustomerId}", dto.CustomerId);

    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId);
    if (customer == null || customer.TripletexId == null || customer.TripletexId == 0)
        throw new InvalidOperationException($"Customer with ID {dto.CustomerId} not found or missing TripletexId.");

    if (!DateOnly.TryParse(dto.OrderDate, out var parsedOrderDate))
        throw new FormatException($"Invalid OrderDate format: '{dto.OrderDate}'");

    var authHeader = await _tokenService.GetAuthorizationAsync();

    var orderPayload = new {
        orderDate = dto.OrderDate,
        deliveryDate = dto.OrderDate,
        customer = new { id = customer.TripletexId },
        invoicesDueIn = 14,
        invoicesDueInType = "DAYS",
        isShowOpenPostsOnInvoices = false,
        orderLineSorting = "PRODUCT",
        isPrioritizeAmountsIncludingVat = false,
        orderLines = new[] {
            new {
                product = new { id = 69691388 },
                description = "Consulting services",
                count = 1,
                unitPriceExcludingVatCurrency = dto.Amount,
                vatType = new { id = 3 }
            }
        }
    };

    var orderRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/order");
    orderRequest.Headers.Add("Authorization", authHeader);
    orderRequest.Content = JsonContent.Create(orderPayload);

    var orderResponse = await _httpClient.SendAsync(orderRequest);
    var orderContent = await orderResponse.Content.ReadAsStringAsync();

    if (!orderResponse.IsSuccessStatusCode)
    {
        _logger.LogError("Order creation failed: {StatusCode} - {Content}", orderResponse.StatusCode, orderContent);
        throw new HttpRequestException($"Failed to create sale order: {orderResponse.StatusCode}");
    }

    var orderResult = JsonSerializer.Deserialize<TripletexSaleOrderResponse>(orderContent, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    var orderId = orderResult?.Value?.Id;
    if (orderId == null)
        throw new Exception("Order ID missing in Tripletex response.");

    _logger.LogInformation("✅ Successfully created order with ID: {OrderId}", orderId);

    var order = new SaleOrder
    {
        TripletexId = (int)orderId,
        Number = dto.Number,
        Status = "Order Created",
        Amount = dto.Amount,
        OrderDate = parsedOrderDate,
        CustomerId = customer.Id
    };

    _context.Saleorders.Add(order);
    await _context.SaveChangesAsync();

    var invoiceId = await CreateInvoiceDirectlyAsync(customer, dto, authHeader);
    
    if (invoiceId > 0)
    {
        order.Status = "Invoice Created";
        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Successfully created invoice {InvoiceId} for Sale Order {OrderId}", invoiceId, orderId);
    }
    else
    {
        _logger.LogWarning("Could not create invoice for Sale Order {OrderId}, but order was created successfully", orderId);
    }

    return new SaleOrderDto
    {
        Id = order.Id,
        Number = order.Number,
        Status = order.Status,
        OrderDate = order.OrderDate.ToDateTime(TimeOnly.MinValue),
        Amount = order.Amount,
        CustomerId = order.CustomerId,
        CustomerName = customer.Name
    };
}

private async Task<int> CreateInvoiceDirectlyAsync(Customer customer, TripletexCreateSaleOrder dto, string authHeader)
{
    try
    {
        var invoiceDate = DateTime.Parse(dto.OrderDate).Date;
        var invoiceDueDate = invoiceDate.AddDays(14);

        _logger.LogInformation("Creating direct invoice with Date: {InvoiceDate}, DueDate: {DueDate}, Amount: {Amount}",
            invoiceDate.ToString("yyyy-MM-dd"), invoiceDueDate.ToString("yyyy-MM-dd"), dto.Amount);

        var invoicePayload = new
        {
            customer = new { id = customer.TripletexId },
            invoiceDate = invoiceDate.ToString("yyyy-MM-dd"),
            invoiceDueDate = invoiceDueDate.ToString("yyyy-MM-dd"),
            currency = new { id = 1 },
            orders = new[]
            {
                new
                {
                    orderDate = invoiceDate.ToString("yyyy-MM-dd"),
                    deliveryDate = invoiceDate.ToString("yyyy-MM-dd"),
                    customer = new { id = customer.TripletexId },
                    invoicesDueIn = 14,
                    invoicesDueInType = "DAYS",
                    isShowOpenPostsOnInvoices = false,
                    orderLineSorting = "PRODUCT",
                    isPrioritizeAmountsIncludingVat = false,
                    orderLines = new[]
                    {
                        new
                        {
                            product = new { id = 69691388 },
                            description = "Consulting services",
                            count = 1,
                            unitPriceExcludingVatCurrency = dto.Amount,
                            vatType = new { id = 3 }
                        }
                    }
                }
            }
        };

        var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/invoice/?invoice.create");
        invoiceRequest.Headers.Add("Authorization", authHeader);
        invoiceRequest.Content = JsonContent.Create(invoicePayload);
        invoiceRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var invoiceResponse = await _httpClient.SendAsync(invoiceRequest);
        var invoiceContent = await invoiceResponse.Content.ReadAsStringAsync();

        if (!invoiceResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Direct invoice creation failed: {StatusCode} - {Error}", invoiceResponse.StatusCode, invoiceContent);
            return 0;
        }

        var invoiceJson = JsonSerializer.Deserialize<JsonElement>(invoiceContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (invoiceJson.TryGetProperty("value", out var value) && 
            value.TryGetProperty("id", out var invoiceIdElement))
        {
            var invoiceId = invoiceIdElement.GetInt32();
            _logger.LogInformation("Successfully created direct invoice with ID: {InvoiceId}", invoiceId);

            await HandleInvoicePostProcessingAsync(invoiceId, authHeader);

var voucherId = await GetVoucherIdFromTripletex(invoiceId, authHeader);
                    if (voucherId > 0)
                    {
                        string pdfPath = Path.Combine(AppContext.BaseDirectory, "invoice.pdf");
                        if (File.Exists(pdfPath))
                        {
                            byte[] fileBytes = await File.ReadAllBytesAsync(pdfPath);
                            string fileName = "invoice.pdf";

                            var uploaded = await UploadVoucherAttachmentAsync(voucherId, fileBytes, fileName, authHeader);
                            if (uploaded)
                            {
                                _logger.LogInformation("Attachment uploaded for voucher {VoucherId}", voucherId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to upload attachment for voucher {VoucherId}", voucherId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Attachment file not found: {Path}", pdfPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No voucher found for invoice {InvoiceId}", invoiceId);
                    }


            return invoiceId;
        }

        _logger.LogError("Invoice ID missing in Tripletex response for direct invoice creation");
        return 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating direct invoice");
        return 0;
    }
}

        private async Task HandleInvoicePostProcessingAsync(int invoiceId, string authHeader)
        {
            try
            {
                var invoiceDetailsUrl = $"https://api-test.tripletex.tech/v2/invoice/{invoiceId}";
                var invoiceDetailsRequest = new HttpRequestMessage(HttpMethod.Get, invoiceDetailsUrl);
                invoiceDetailsRequest.Headers.Add("Authorization", authHeader);
                var detailsResponse = await _httpClient.SendAsync(invoiceDetailsRequest);
                var detailsContent = await detailsResponse.Content.ReadAsStringAsync();

                bool isCharged = false;
                bool isApproved = false;

                if (detailsResponse.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(detailsContent);
                    if (jsonDoc.RootElement.TryGetProperty("value", out var valueEl))
                    {
                        isCharged = valueEl.TryGetProperty("isCharged", out var chargedEl) && chargedEl.GetBoolean();
                        isApproved = valueEl.TryGetProperty("isApproved", out var approvedEl) && approvedEl.GetBoolean();

                        _logger.LogInformation("Invoice {InvoiceId} status: Charged={IsCharged}, Approved={IsApproved}",
                            invoiceId, isCharged, isApproved);
                    }
                }

                if (!isCharged)
                {
                    if (!isApproved)
                    {
                        var approved = await ApproveInvoiceAsync(invoiceId, authHeader);
                        if (approved)
                        {
                            _logger.LogInformation("Invoice {InvoiceId} approved successfully", invoiceId);
                        }
                    }

                    // Send the invoice
                    await SendInvoiceAsync(invoiceId, authHeader);
                }
                else
                {
                    _logger.LogInformation("ℹ️ Invoice {InvoiceId} is already charged. Skipping approval and send.", invoiceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in post-processing for invoice {InvoiceId}", invoiceId);
            }
        }


        private async Task<int> GetVoucherIdFromTripletex(int invoiceId, string authHeader)
        {
            var url = $"https://api-test.tripletex.tech/v2/invoice/{invoiceId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return 0;

            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("value", out var value) &&
                value.TryGetProperty("voucher", out var voucher) &&
                voucher.TryGetProperty("id", out var voucherIdElement))
            {
                return voucherIdElement.GetInt32();
            }

            return 0;
        }


        private async Task<bool> UploadVoucherAttachmentAsync(int voucherId, byte[] fileBytes, string fileName, string authHeader)
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}/attachment");
            request.Headers.Add("Authorization", authHeader);
            request.Content = form;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("📎 Tripletex upload response: {Status} - {Body}", response.StatusCode, responseBody);

            return response.IsSuccessStatusCode;
        }





        private async Task<bool> ApproveInvoiceAsync(int invoiceId, string authHeader)
        {
            try
            {
                var url = $"https://api-test.tripletex.tech/v2/invoice/{invoiceId}/approve";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to approve invoice {InvoiceId}. Status: {StatusCode} - {Body}",
                        invoiceId, response.StatusCode, content);
                    return false;
                }

                _logger.LogInformation("Invoice {InvoiceId} approved successfully", invoiceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving invoice {InvoiceId}", invoiceId);
                return false;
            }
        }

        private async Task SendInvoiceAsync(int invoiceId, string authHeader)
        {
            try
            {
                var sendUrl = $"https://api-test.tripletex.tech/v2/invoice/{invoiceId}/send";
                var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendUrl);
                sendRequest.Headers.Add("Authorization", authHeader);

                var sendResponse = await _httpClient.SendAsync(sendRequest);
                var sendContent = await sendResponse.Content.ReadAsStringAsync();

                if (!sendResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to send invoice {InvoiceId}. Status: {Status}, Response: {Response}",
                        invoiceId, sendResponse.StatusCode, sendContent);
                }
                else
                {
                    _logger.LogInformation("Invoice {InvoiceId} successfully sent. Response: {Response}",
                        invoiceId, sendContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invoice {InvoiceId} via Tripletex");
            }
        }
        public async Task ImportSaleOrdersAsync()
        {
            await SyncSaleOrdersFromTripletexAsync();
        }
    }

    public class SaleOrderListResponseAlternative
    {
        public List<SaleOrderDto> Values { get; set; } = new();
    }
}
