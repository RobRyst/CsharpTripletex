using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using backend.Infrastructure.Data;
using backend.Mappers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                        _logger.LogInformation("Added new order: {OrderNumber}", dto.Number);
                    }
                    else
                    {
                        SaleOrderMapper.UpdateEntity(existing, dto);
                        _logger.LogInformation("Updated existing order: {OrderNumber}", dto.Number);
                    }
                }

                _logger.LogInformation("Saving changes to database...");
                await _context.SaveChangesAsync();
                await Task.Delay(2000);
                _logger.LogInformation("Save successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sales order sync");
                throw;
            }
        }

        public async Task<List<SaleOrderDto>> GetSaleOrdersFromTripletexAsync()
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();
            var fromDate = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd");
            var toDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            var url = $"https://api.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}&fields=id,number,status,orderDate,customer(id,name,email)";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "YourApp/1.0");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching sales orders: {StatusCode} - {Error}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Sales order fetch failed: {response.StatusCode} - {responseContent}");
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
        throw new InvalidOperationException($"Customer with TripletexId {dto.CustomerId} not found or missing TripletexId.");

    if (!DateOnly.TryParse(dto.OrderDate, out var parsedOrderDate))
        throw new FormatException($"Invalid OrderDate format: '{dto.OrderDate}'");

    var payload = new
    {
        customer = new { id = customer.TripletexId },
        orderDate = parsedOrderDate.ToString("yyyy-MM-dd"),
        deliveryDate = parsedOrderDate.ToString("yyyy-MM-dd"),
        isPrioritizeAmountsIncludingVat = false,
        orderLines = new[]
        {
            new {
                product = new { id = 69691388 },
                description = "Consulting services",
                count = 1,
                unitPriceExcludingVatCurrency = dto.Amount,
                vatType = new { id = 3 }
            }
        }
    };

    var authHeader = await _tokenService.GetAuthorizationAsync();

    // Step 1: Create Sale Order in Tripletex
    var orderRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/order")
    {
        Content = JsonContent.Create(payload)
    };
    orderRequest.Headers.Add("Authorization", authHeader);
    orderRequest.Headers.Add("Accept", "application/json");

    var orderResponseHttp = await _httpClient.SendAsync(orderRequest);
    var orderContent = await orderResponseHttp.Content.ReadAsStringAsync();

    if (!orderResponseHttp.IsSuccessStatusCode)
    {
        _logger.LogError("Tripletex order creation failed: {Response}", orderContent);
        throw new HttpRequestException($"Failed to create order: {orderContent}");
    }

    var tripletexResponse = JsonSerializer.Deserialize<TripletexOrderResponse>(orderContent, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (tripletexResponse?.Value?.Id == null)
        throw new Exception("Tripletex response missing order ID.");

    var tripletexId = tripletexResponse.Value.Id;
    _logger.LogInformation("Tripletex order ID: {TripletexId}", tripletexId);

    // Step 2: Save SaleOrder locally
    var order = new SaleOrder
    {
        TripletexId = (int)tripletexId,
        Number = dto.Number,
        Status = "Created",
        Amount = dto.Amount,
        OrderDate = parsedOrderDate,
        CustomerId = customer.Id
    };
    _context.Saleorders.Add(order);
    await _context.SaveChangesAsync();
    await Task.Delay(2000);

    _logger.LogInformation("SaleOrder saved locally with Tripletex ID {TripletexId}", tripletexId);

    // Step 3: Check if Tripletex already created a preliminary invoice
    var orderGetRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api-test.tripletex.tech/v2/order/{tripletexId}");
    orderGetRequest.Headers.Add("Authorization", authHeader);
    orderGetRequest.Headers.Add("Accept", "application/json");

    var orderGetResponse = await _httpClient.SendAsync(orderGetRequest);
    var orderGetContent = await orderGetResponse.Content.ReadAsStringAsync();

    if (!orderGetResponse.IsSuccessStatusCode)
    {
        _logger.LogWarning("Failed to retrieve order to check for existing invoice: {Response}", orderGetContent);
    }

    var tripletexOrderResponse = JsonSerializer.Deserialize<TripletexOrderGetResponse>(orderGetContent, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

TripletexInvoiceResponse? invoiceResponse = null;

// Retry up to 3 times
for (int attempt = 1; attempt <= 3; attempt++)
{
    var invoiceRequest = new HttpRequestMessage(
        HttpMethod.Post,
        $"https://api-test.tripletex.tech/v2/order/{tripletexId}/invoice?sendInvoice=true")
    {
        Content = JsonContent.Create(new { }) // empty payload
    };
    invoiceRequest.Headers.Add("Authorization", authHeader);
    invoiceRequest.Headers.Add("Accept", "application/json");

    var invoiceHttpResponse = await _httpClient.SendAsync(invoiceRequest);
    var invoiceContent = await invoiceHttpResponse.Content.ReadAsStringAsync();

    if (invoiceHttpResponse.IsSuccessStatusCode)
    {
        invoiceResponse = JsonSerializer.Deserialize<TripletexInvoiceResponse>(invoiceContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        _logger.LogInformation("Invoice successfully created and finalized for order {OrderId} on attempt {Attempt}", tripletexId, attempt);
        _logger.LogInformation("Invoice response content: {Content}", invoiceContent);
        break;
    }

    if (attempt < 3)
    {
        _logger.LogWarning("Invoice creation attempt {Attempt} failed, retrying in {Delay}s... Error: {Error}", attempt, attempt, invoiceContent);
        await Task.Delay(1000 * attempt); // 1s, 2s, 3s...
    }
    else
    {
        _logger.LogError("Failed to finalize invoice after 3 attempts: {Error}", invoiceContent);
        throw new HttpRequestException($"Failed to finalize invoice: {invoiceHttpResponse.StatusCode}");
    }
}
    // Step 4: Save invoice locally if it exists
    if (invoiceResponse?.Value != null)
    {
        var invoice = new Invoice
        {
            TripletexId = (int)invoiceResponse.Value.Id,
            Status = "Created",
            Total = invoiceResponse.Value.Amount,
            InvoiceCreated = DateOnly.FromDateTime(DateTime.UtcNow),
            InvoiceDate = DateOnly.Parse(invoiceResponse.Value.InvoiceDate),
            InvoiceDueDate = DateOnly.Parse(invoiceResponse.Value.InvoiceDueDate),
            DueDate = DateOnly.Parse(invoiceResponse.Value.InvoiceDueDate),
            Currency = "NOK",
            CustomerId = order.CustomerId,
            CustomerTripletexId = customer.TripletexId
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        await Task.Delay(2000);
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
