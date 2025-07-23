using System.Text.Json;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using System.Text;
using backend.Domain.Entities;
using backend.Domain.Models;
using backend.Mappers;
using System.Net.Http.Headers;
using backend.Infrastructure.Data;

namespace backend.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<InvoiceService> _logger;
        private readonly AppDbContext _db;

        public InvoiceService(
                    HttpClient httpClient,
                    ITokenService tokenService,
                    IInvoiceRepository invoiceRepository,
                    ICustomerRepository customerRepository,
                    ILogger<InvoiceService> logger,
                    AppDbContext db)
                {
                    _httpClient = httpClient;
                    _tokenService = tokenService;
                    _invoiceRepository = invoiceRepository;
                    _customerRepository = customerRepository;
                    _logger = logger;
                    _db = db;
                }

        public async Task<IEnumerable<InvoiceModel>> GetAllAsync()
        {
            var invoices = await _invoiceRepository.GetAllAsync();
            return invoices.Select(InvoiceMapper.ToModel);
        }

        public async Task<IEnumerable<InvoiceModel>> GetAllInvoicesAsync()
        {
            var invoices = await _invoiceRepository.GetAllWithCustomerAsync();
            return invoices.Select(InvoiceMapper.ToModel);
        }

        public async Task<InvoiceModel> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(id);
            if (invoice == null)
            {
                throw new KeyNotFoundException($"Invoice with id {id} not found");
            }
            return InvoiceMapper.ToModel(invoice);
        }

        public async Task<string> GetAuthorizationAsync()
        {
            return await _tokenService.GetAuthorizationAsync();
        }

        public async Task<List<InvoiceModel>> GetInvoicesFromTripletexAsync()
        {
            try
            {
                var fromDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
                var toDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");

                var url = $"https://api-test.tripletex.tech/v2/invoice?invoiceDateFrom={fromDate}&invoiceDateTo={toDate}";
                var authHeader = await _tokenService.GetAuthorizationAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", authHeader);

                _logger.LogInformation("Fetching invoices from Tripletex API from {FromDate} to {ToDate}", fromDate, toDate);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error fetching invoices: {StatusCode} - {Error}", response.StatusCode, content);
                    throw new HttpRequestException($"Invoice fetch failed: {response.StatusCode}");
                }

                var invoiceEntities = await ParseTripletexInvoiceResponse(content);
                return invoiceEntities.Select(InvoiceMapper.ToModel).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices from Tripletex");
                throw;
            }
        }

        public async Task SyncInvoicesFromTripletexAsync()
        {
            try
            {
                _logger.LogInformation("Starting invoice synchronization from Tripletex");
                var invoicesFromApi = await GetInvoicesFromTripletexAsync();
                _logger.LogInformation("Found {Count} invoices from Tripletex API", invoicesFromApi.Count);
                var entityInvoices = invoicesFromApi.Select(InvoiceMapper.ToEntity).ToList();
                await _invoiceRepository.BulkUpsertAsync(entityInvoices);
                _logger.LogInformation("Invoice synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during invoice synchronization");
                throw;
            }
        }

        public async Task<int> CreateInvoiceInTripletexAsync(TripletexInvoiceCreateDto dto, byte[] fileBytes, string fileName, string userId)
        {
            try
            {
                var authHeader = await _tokenService.GetAuthorizationAsync();

                var customer = await _customerRepository.GetByTripletexIdAsync(dto.Customer.Id);
                if (customer == null || customer.TripletexId == 0)
                {
                    throw new InvalidOperationException($"Customer with TripletexId {dto.Customer.Id} does not exist in local DB or has invalid ID");
                }

                var invoiceDate = string.IsNullOrEmpty(dto.InvoiceDate)
                    ? DateTime.UtcNow.Date
                    : DateTime.Parse(dto.InvoiceDate).Date;
                var invoiceDueDate = string.IsNullOrEmpty(dto.InvoiceDueDate)
                    ? DateTime.UtcNow.AddDays(14).Date
                    : DateTime.Parse(dto.InvoiceDueDate).Date;

                _logger.LogInformation("Creating invoice with Date: {InvoiceDate}, DueDate: {DueDate}, Amount: {Amount}",
                    invoiceDate.ToString("yyyy-MM-dd"), invoiceDueDate.ToString("yyyy-MM-dd"), dto.Amount);

                var invoiceAmount = dto.Amount > 0 ? dto.Amount : 5000;

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
                            description = dto.Description ?? "Consulting services",
                            count = 1,
                            unitPriceExcludingVatCurrency = invoiceAmount,
                            vatType = new { id = 3 }
                        }
                    }
                }
            }
                };

                // Create the invoice
                var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/invoice/?invoice.create");
                invoiceRequest.Headers.Add("Authorization", authHeader);
                invoiceRequest.Content = JsonContent.Create(invoicePayload);
                invoiceRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var invoiceResponse = await _httpClient.SendAsync(invoiceRequest);
                var invoiceContent = await invoiceResponse.Content.ReadAsStringAsync();

                if (!invoiceResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Invoice creation failed: {StatusCode} - {Error}", invoiceResponse.StatusCode, invoiceContent);
                    throw new HttpRequestException($"Invoice creation failed: {invoiceResponse.StatusCode} - {invoiceContent}");
                }

                var invoiceJson = JsonSerializer.Deserialize<TripletexResponseDto>(invoiceContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var invoiceId = invoiceJson?.Value?.Id ?? 0;
                _logger.LogInformation("✅ Successfully created invoice with ID: {InvoiceId}", invoiceId);

                // Store invoice details in local DB
                await LogCreatedInvoiceDetails((int)invoiceId, authHeader);

                // Handle attachment upload if provided
                if (!string.IsNullOrWhiteSpace(fileName) && fileBytes?.Length > 0)
                {
                    _logger.LogInformation("📎 Uploading attachment for invoice {InvoiceId} (file: {FileName}, size: {FileSize} bytes)",
                        invoiceId, fileName, fileBytes.Length);

                    var voucherId = await GetVoucherIdFromTripletex((int)invoiceId, authHeader);

                    if (voucherId > 0)
                    {
                        _logger.LogInformation("Found voucher ID {VoucherId} for invoice {InvoiceId}", voucherId, invoiceId);

                        var success = await UploadVoucherAttachmentAsync(voucherId, fileBytes, fileName, userId);
                        if (success)
                        {
                            _logger.LogInformation("✅ Attachment uploaded successfully for voucher {VoucherId}", voucherId);

                            // Wait a moment for the attachment to be processed
                            await Task.Delay(1000);

                            // Verify the attachment was properly linked
                            var verified = await VerifyInvoiceAttachmentAsync((int)invoiceId);
                            if (verified)
                            {
                                _logger.LogInformation("✅ Attachment verified and linked to invoice {InvoiceId}", invoiceId);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Attachment upload succeeded but verification failed for invoice {InvoiceId}", invoiceId);
                            }
                        }
                        else
                        {
                            _logger.LogError("❌ Failed to upload attachment for voucher {VoucherId}", voucherId);
                        }
                    }
                    else
                    {
                        _logger.LogError("❌ Could not retrieve voucher ID for invoice {InvoiceId}. Cannot upload attachment.", invoiceId);
                    }
                }

                // Check invoice status and handle approval/sending if needed
                await HandleInvoicePostProcessing((int)invoiceId, authHeader);

                return (int)invoiceId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating invoice in Tripletex");
                throw;
            }
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

                _logger.LogInformation("✅ Invoice {InvoiceId} approved successfully", invoiceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving invoice {InvoiceId}", invoiceId);
                return false;
            }
        }
        public async Task<bool> VerifyInvoiceAttachmentAsync(int invoiceId)
        {
            try
            {
                var authHeader = await _tokenService.GetAuthorizationAsync();

                // Get the voucher ID first
                var voucherId = await GetVoucherIdFromTripletex(invoiceId, authHeader);
                if (voucherId == 0)
                {
                    _logger.LogWarning("No voucher found for invoice {InvoiceId}", invoiceId);
                    return false;
                }

                // Check if voucher has attachment
                var url = $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch voucher {VoucherId}: {StatusCode} - {Content}",
                        voucherId, response.StatusCode, content);
                    return false;
                }

                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    // Check for attachment
                    if (valueElement.TryGetProperty("attachment", out var attachmentElement) &&
                        attachmentElement.TryGetProperty("id", out var attachmentIdElement))
                    {
                        var attachmentId = attachmentIdElement.GetInt32();
                        _logger.LogInformation("Attachment found for invoice {InvoiceId}: Attachment ID = {AttachmentId}",
                            invoiceId, attachmentId);


                        _logger.LogDebug("Voucher {VoucherId} details: {VoucherData}", voucherId, content);

                        return true;
                    }
                }

                _logger.LogWarning("❌ No attachment found for invoice {InvoiceId} (voucher {VoucherId})", invoiceId, voucherId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying attachment for invoice {InvoiceId}", invoiceId);
                return false;
            }
        }


        public async Task<object> GetInvoiceAttachmentDetailsAsync(int invoiceId)
        {
            try
            {
                var authHeader = await _tokenService.GetAuthorizationAsync();
                var voucherId = await GetVoucherIdFromTripletex(invoiceId, authHeader);

                if (voucherId == 0)
                {
                    return new { success = false, message = "No voucher found for invoice" };
                }

                var url = $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, message = $"API error: {response.StatusCode}" };
                }

                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    var result = new
                    {
                        success = true,
                        invoiceId = invoiceId,
                        voucherId = voucherId,
                        hasDocument = valueElement.TryGetProperty("document", out var docEl) && docEl.TryGetProperty("id", out _),
                        hasAttachment = valueElement.TryGetProperty("attachment", out var attachEl) && attachEl.TryGetProperty("id", out _),
                        documentId = valueElement.TryGetProperty("document", out var docElement) && docElement.TryGetProperty("id", out var docIdEl) ? docIdEl.GetInt32() : (int?)null,
                        attachmentId = valueElement.TryGetProperty("attachment", out var attElement) && attElement.TryGetProperty("id", out var attIdEl) ? attIdEl.GetInt32() : (int?)null,
                        rawData = JsonSerializer.Serialize(valueElement, new JsonSerializerOptions { WriteIndented = true })
                    };

                    return result;
                }

                return new { success = false, message = "Invalid response format" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attachment details for invoice {InvoiceId}", invoiceId);
                return new { success = false, message = ex.Message };
            }
        }

        private async Task HandleInvoicePostProcessing(int invoiceId, string authHeader)
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

                // Only try to approve/send if the invoice is not already charged
                if (!isCharged)
                {
                    if (!isApproved)
                    {
                        var approved = await ApproveInvoiceAsync(invoiceId, authHeader);
                        if (approved)
                        {
                            _logger.LogInformation("✅ Invoice {InvoiceId} approved successfully", invoiceId);
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

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch voucher for invoice {InvoiceId}. Response: {StatusCode} - {Content}", invoiceId, response.StatusCode, content);
                return 0;
            }

            try
            {
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("value", out var value) &&
                    value.TryGetProperty("voucher", out var voucher) &&
                    voucher.TryGetProperty("id", out var voucherIdElement))
                {
                    return voucherIdElement.GetInt32();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing voucherId from Tripletex invoice response for invoice {InvoiceId}", invoiceId);
            }

            return 0;
        }
        public async Task<int> CreateInvoiceWithAttachmentAsync(TripletexInvoiceCreateDto dto)
        {
            string pdfPath = Path.Combine(AppContext.BaseDirectory, "invoice.pdf");

            if (!File.Exists(pdfPath))
            {
                _logger.LogError("Fant ikke PDF-filen: {Path}", pdfPath);
                throw new FileNotFoundException("Test-PDF ikke funnet", pdfPath);
            }

            byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            string fileName = "invoice.pdf";

            _logger.LogInformation("Leste PDF fra disk, størrelse: {Size} bytes", pdfBytes.Length);

            return await CreateInvoiceInTripletexAsync(dto, pdfBytes, fileName, "system");
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

                private async Task<int> GenerateInvoiceFromOrderAsync(int orderId, string authHeader)
        {
            var url = $"https://api-test.tripletex.tech/v2/order/{orderId}/invoice";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", authHeader);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to generate invoice from order {OrderId}: {StatusCode} - {Content}", orderId, response.StatusCode, content);
                throw new HttpRequestException("Invoice generation failed");
            }

            var jsonDoc = JsonDocument.Parse(content);
            var invoiceId = jsonDoc.RootElement.GetProperty("value").GetProperty("id").GetInt32();
            _logger.LogInformation("Invoice {InvoiceId} created from Order {OrderId}", invoiceId, orderId);
            return invoiceId;
        }

        private async Task LogCreatedInvoiceDetails(int invoiceId, string authHeader)
        {
            try
            {
                var url = $"https://api-test.tripletex.tech/v2/invoice/{invoiceId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Created invoice details for ID {InvoiceId}: {Response}", invoiceId, content);

                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    var parsedInvoice = await ParseSingleInvoiceFromTripletex(valueElement);
                    if (parsedInvoice != null)
                    {
                        await _invoiceRepository.BulkUpsertAsync(new List<Invoice> { parsedInvoice });
                        _logger.LogInformation("Stored created invoice with ID {InvoiceId} in local DB.", invoiceId);
                    }

                    decimal amount = valueElement.TryGetProperty("amountIncludingVat", out var amountElement)
                    ? amountElement.GetDecimal()
                    : 0;

                    string orderIds = "";
                    if (valueElement.TryGetProperty("orders", out var ordersElement))
                    {
                        var orderList = ordersElement.EnumerateArray().Select(o =>
                            o.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "N/A");
                        orderIds = string.Join(", ", orderList);
                    }

                    _logger.LogInformation("Invoice {InvoiceId} created with Amount={Amount}, Orders=[{Orders}]",
                        invoiceId, amount, orderIds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch or store created invoice details");
            }
        }


       private async Task<IEnumerable<Invoice>> ParseTripletexInvoiceResponse(string jsonResponse)
        {
            var invoices = new List<Invoice>();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (!jsonDoc.RootElement.TryGetProperty("values", out var valuesElement))
                {
                    _logger.LogWarning("No 'values' property found in Tripletex response");
                    return invoices;
                }

                foreach (var invoiceElement in valuesElement.EnumerateArray())
                {
                    if (!invoiceElement.TryGetProperty("customer", out var customerElement) ||
                        !customerElement.TryGetProperty("id", out var customerIdElement))
                    {
                        _logger.LogWarning("Invoice missing customer information, skipping");
                        continue;
                    }

                    var customerTripletexId = customerIdElement.GetInt32();
                    var customer = await _customerRepository.GetByTripletexIdAsync(customerTripletexId);

                    if (customer == null)
                    {
                        _logger.LogWarning("Customer with TripletexId {TripletexId} not found, skipping invoice", customerTripletexId);
                        continue;
                    }

                    _logger.LogDebug("Invoice element: {InvoiceElement}", invoiceElement.GetRawText());

                    string status = "Unknown";

                    if (invoiceElement.TryGetProperty("state", out var stateElement))
                    {
                        var state = stateElement.GetString();
                        status = MapTripletexStatusToDisplayStatus(state);
                    }

                    if (status == "Unknown")
                    {
                        if (invoiceElement.TryGetProperty("sendMethodDescription", out var sendMethodElement))
                        {
                            status = sendMethodElement.GetString() ?? "Unknown";
                        }
                        else if (invoiceElement.TryGetProperty("status", out var statusElement))
                        {
                            status = statusElement.GetString() ?? "Unknown";
                        }
                    }

                    decimal amount = 0;
                    if (invoiceElement.TryGetProperty("amount", out var amountElement))
                    {
                        amount = amountElement.GetDecimal();
                    }
                    else if (invoiceElement.TryGetProperty("amountIncludingVat", out var amountInclElement))
                    {
                        amount = amountInclElement.GetDecimal();
                    }
                    else if (invoiceElement.TryGetProperty("amountExcludingVat", out var amountExclElement))
                    {
                        amount = amountExclElement.GetDecimal();
                    }

                    var displayStatus = $"{status} {amount:N2}";

                    var invoice = new Invoice
                    {
                        TripletexId = invoiceElement.GetProperty("id").GetInt32(),
                        Status = displayStatus,
                        Total = amount,
                        InvoiceCreated = invoiceElement.TryGetProperty("invoiceDate", out var invoiceDateElement) ?
                            DateOnly.FromDateTime(invoiceDateElement.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow),
                        InvoiceDueDate = invoiceElement.TryGetProperty("dueDate", out var dueDateElement) ?
                            DateOnly.FromDateTime(dueDateElement.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                        CustomerId = customer.Id,
                        InvoiceDate = invoiceElement.TryGetProperty("invoiceDate", out var invDateElement) ?
                            DateOnly.FromDateTime(invDateElement.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow),
                        DueDate = invoiceElement.TryGetProperty("dueDate", out var dueDateElem) ?
                            DateOnly.FromDateTime(dueDateElem.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))
                    };

                    _logger.LogDebug("Parsed invoice: ID={TripletexId}, Status={Status}, Total={Total}",
                        invoice.TripletexId, invoice.Status, invoice.Total);

                    invoices.Add(invoice);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing JSON response from Tripletex");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Tripletex invoice response");
                throw;
            }

            return invoices;
        }

        private async Task<Invoice?> ParseSingleInvoiceFromTripletex(JsonElement valueElement)
        {
            if (!valueElement.TryGetProperty("customer", out var customerElement) ||
                !customerElement.TryGetProperty("id", out var customerIdElement))
            {
                _logger.LogWarning("Invoice missing customer info in single parse, skipping.");
                return null;
            }

            var customerTripletexId = customerIdElement.GetInt32();
            var customer = await _customerRepository.GetByTripletexIdAsync(customerTripletexId);
            if (customer == null)
            {
                _logger.LogWarning("Customer with TripletexId {TripletexId} not found for single invoice parse", customerTripletexId);
                return null;
            }

            int? voucherId = null;
            if (valueElement.TryGetProperty("voucher", out var voucherElement) &&
                voucherElement.ValueKind == JsonValueKind.Object &&
                voucherElement.TryGetProperty("id", out var voucherIdElement) &&
                voucherIdElement.ValueKind == JsonValueKind.Number)
            {
                voucherId = voucherIdElement.GetInt32();
            }

            string status = "Unknown";
            if (valueElement.TryGetProperty("state", out var stateElement))
            {
                var state = stateElement.GetString();
                status = MapTripletexStatusToDisplayStatus(state);
            }

            decimal amount = 0;
            if (valueElement.TryGetProperty("amountIncludingVat", out var amountIncl))
            {
                amount = amountIncl.GetDecimal();
            }
            else if (valueElement.TryGetProperty("amount", out var amountElement))
            {
                amount = amountElement.GetDecimal();
            }

            var displayStatus = $"{status} {amount:N2}";

            return new Invoice
            {
                TripletexId = valueElement.GetProperty("id").GetInt32(),
                VoucherId = voucherId,
                Status = displayStatus,
                Total = amount,
                InvoiceCreated = valueElement.TryGetProperty("invoiceDate", out var invoiceDateEl)
                    ? DateOnly.FromDateTime(invoiceDateEl.GetDateTime())
                    : DateOnly.FromDateTime(DateTime.UtcNow),
                InvoiceDueDate = valueElement.TryGetProperty("invoiceDueDate", out var dueDateEl)
                    ? DateOnly.FromDateTime(dueDateEl.GetDateTime())
                    : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                CustomerId = customer.Id,
                InvoiceDate = valueElement.TryGetProperty("invoiceDate", out var invDateEl)
                    ? DateOnly.FromDateTime(invDateEl.GetDateTime())
                    : DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate = valueElement.TryGetProperty("invoiceDueDate", out var dueDate2El)
                    ? DateOnly.FromDateTime(dueDate2El.GetDateTime())
                    : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))
            };

        }

                public async Task UpdateVoucherIdsForExistingInvoicesAsync()
                {
                    var invoices = await _invoiceRepository.GetAllWithCustomerAsync();

                    foreach (var inv in invoices.Where(i => i.TripletexId != null && i.VoucherId == 0))
                    {
                        var url = $"https://api-test.tripletex.tech/v2/invoice/{inv.TripletexId}";
                        var authHeader = await _tokenService.GetAuthorizationAsync();

                        var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Add("Authorization", authHeader);

                        var response = await _httpClient.SendAsync(request);
                        var content = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonDoc = JsonDocument.Parse(content);
                            if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement) &&
                                valueElement.TryGetProperty("voucher", out var voucherElement) &&
                                voucherElement.TryGetProperty("id", out var voucherIdElement))
                            {
                                inv.VoucherId = voucherIdElement.GetInt32();
                                _logger.LogInformation("Updated VoucherId for invoice {InvoiceId}: {VoucherId}", inv.Id, inv.VoucherId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not fetch voucher for invoice {TripletexId}", inv.TripletexId);
                        }
                    }

                    await _db.SaveChangesAsync();
                }

                public async Task<bool> UploadVoucherAttachmentAsync(int voucherId, byte[] fileBytes, string fileName, string userId)
                {
                    var authHeader = await _tokenService.GetAuthorizationAsync();

                    using var form = new MultipartFormDataContent();

                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");

                    form.Add(fileContent, "file", fileName);


                    form.Add(fileContent, "file", fileName);

                    var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}/attachment");
                    request.Headers.Add("Authorization", authHeader);
                    request.Content = form;

                    var response = await _httpClient.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();

                    _logger.LogInformation("📎 Tripletex response: {Code} - {Body}", response.StatusCode, body);

                    return response.IsSuccessStatusCode;
                }


        private string GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }

        public async Task<bool> ConfirmAttachmentLinkedAsync(int voucherId)
        {
            try
            {
                var authHeader = await _tokenService.GetAuthorizationAsync();
                var url = $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch voucher {VoucherId}: {StatusCode} - {Body}", voucherId, response.StatusCode, content);
                    return false;
                }

                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement) &&
                    valueElement.TryGetProperty("attachment", out var attachmentElement) &&
                    attachmentElement.TryGetProperty("id", out var attachmentIdElement))
                {
                    var attachedFileName = attachmentElement.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString()
                        : "unknown";

                    _logger.LogInformation("Attachment found for voucher {VoucherId}: File ID = {AttachmentId}, Name = {FileName}",
                        voucherId, attachmentIdElement.GetInt32(), attachedFileName);

                    return true;
                }

                _logger.LogWarning("No attachment found on voucher {VoucherId}", voucherId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while checking attachment for voucher {VoucherId}", voucherId);
                return false;
            }
        }

        public async Task<int> CreateInvoiceInTripletexAsync(TripletexInvoiceCreateDto dto)
        {
            string pdfPath = Path.Combine(AppContext.BaseDirectory, "invoice.pdf");
            if (!File.Exists(pdfPath))
            {
                _logger.LogWarning("invoice.pdf file not found at path: {Path}. Skipping attachment.", pdfPath);
                return await CreateInvoiceInTripletexAsync(dto, Array.Empty<byte>(), string.Empty, string.Empty);
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(pdfPath);
            return await CreateInvoiceInTripletexAsync(dto, fileBytes, "invoice.pdf", "system");
        }

        private string MapTripletexStatusToDisplayStatus(string? tripletexState)
        {
            return tripletexState?.ToUpper() switch
            {
                "DRAFT" => "Draft",
                "OPEN" => "Invoice must be sent manually.",
                "SENT" => "Sent",
                "PAID" => "Paid",
                "CANCELLED" => "Cancelled",
                "CREDIT_NOTE" => "Credit note",
                "OVERDUE" => "Overdue",
                "REMINDER" => "Reminder sent",
                null => "Unknown",
                _ => tripletexState
            };
        }
    }
}