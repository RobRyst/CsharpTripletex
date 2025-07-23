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

        var invoiceModel = InvoiceMapper.FromTripletexDto(dto, customer.Id);

        var invoicePayload = new
        {
            customer = new { id = customer.TripletexId },
            invoiceDate = invoiceModel.InvoiceDate.ToString("yyyy-MM-dd"),
            invoiceDueDate = invoiceModel.InvoiceDueDate.ToString("yyyy-MM-dd"),
            currency = new { id = 1 },
            orders = new[]
            {
                new
                {
                    orderDate = invoiceModel.InvoiceDate.ToString("yyyy-MM-dd"),
                    deliveryDate = invoiceModel.InvoiceDate.ToString("yyyy-MM-dd"),
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
                            unitPriceExcludingVatCurrency = invoiceModel.Total,
                            vatType = new { id = 3 }
                        }
                    }
                }
            }
        };

        var serialized = JsonSerializer.Serialize(invoicePayload, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("🧾 Payload being sent to Tripletex:\n{Payload}", serialized);

        var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/invoice/?invoice.create");
        invoiceRequest.Headers.Add("Authorization", authHeader);
        invoiceRequest.Content = JsonContent.Create(invoicePayload);
        invoiceRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _logger.LogInformation("Creating invoice with payload: {Payload}", JsonSerializer.Serialize(invoicePayload, new JsonSerializerOptions { WriteIndented = true }));

        var invoiceResponse = await _httpClient.SendAsync(invoiceRequest);
        var invoiceContent = await invoiceResponse.Content.ReadAsStringAsync();

        _logger.LogInformation("Invoice creation response: {Status} - {Body}", invoiceResponse.StatusCode, invoiceContent);

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
        int orderId = 0;
        if (invoiceJson?.Value?.Orders != null && invoiceJson.Value.Orders.Count > 0)
        {
            orderId = (int)invoiceJson.Value.Orders[0].Id;
        }

        if (invoiceId > 0)
        {
            _logger.LogInformation("Invoice already created with ID: {InvoiceId}, skipping generation from order.", invoiceId);
        }
        else if (orderId > 0)
        {
            invoiceId = await GenerateInvoiceFromOrderAsync(orderId, authHeader);
        }
        else
        {
            _logger.LogWarning("No order or invoice ID returned. Cannot proceed.");
        }
        
        if (invoiceId == 0)
        {
            throw new Exception("Invoice ID could not be extracted from Tripletex response.");
        }

        _logger.LogInformation("Successfully created invoice with ID: {InvoiceId}", invoiceId);
        
        // Store invoice details in local DB first
        await LogCreatedInvoiceDetails((int)invoiceId, authHeader);
        
        // Send the invoice
        await SendInvoiceAsync((int)invoiceId, authHeader);

        // Handle attachment upload if file is provided
        if (!string.IsNullOrWhiteSpace(fileName) && fileBytes?.Length > 0)
        {
            _logger.LogInformation("Attempting to upload attachment for invoice {InvoiceId}", invoiceId);
            
            // Get the voucher ID from Tripletex API
            var voucherId = await GetVoucherIdFromTripletex((int)invoiceId, authHeader);
            
            if (voucherId > 0)
            {
                _logger.LogInformation("Found voucher ID {VoucherId} for invoice {InvoiceId}", voucherId, invoiceId);
                
                var success = await UploadVoucherAttachmentAsync(voucherId, fileBytes, fileName, userId);
                if (success)
                {
                    _logger.LogInformation("Attachment uploaded successfully for voucher {VoucherId}", voucherId);
                    
                    // Confirm the attachment was linked
                    var confirmed = await ConfirmAttachmentLinkedAsync(voucherId);
                    if (confirmed)
                    {
                        _logger.LogInformation("Attachment confirmed and linked to voucher {VoucherId}", voucherId);
                    }
                    else
                    {
                        _logger.LogWarning("Upload succeeded but attachment not confirmed for voucher {VoucherId}", voucherId);
                    }
                }
                else
                {
                    _logger.LogError("Failed to upload attachment for voucher {VoucherId}", voucherId);
                }
            }
            else
            {
                _logger.LogError("Could not retrieve voucher ID for invoice {InvoiceId}. Cannot upload attachment.", invoiceId);
            }
        }
        else
        {
            _logger.LogInformation("No attachment provided for invoice {InvoiceId}, skipping upload", invoiceId);
        }

        return (int)invoiceId;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating invoice in Tripletex");
        throw;
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

        public void GenerateInvoicePdfFile()
        {
            string invoicePdfPath = Path.Combine(AppContext.BaseDirectory, "invoice.pdf");
            InvoicePdfGenerator.GenerateInvoicePdf(invoicePdfPath);
        }
        public async Task<int> CreateInvoiceWithAttachmentAsync(TripletexInvoiceCreateDto dto)
        {
            GenerateInvoicePdfFile();
            string binFolder = AppContext.BaseDirectory;
            string invoicePdfPath = Path.Combine(binFolder, "invoice.pdf");

            InvoicePdfGenerator.GenerateInvoicePdf(invoicePdfPath);
            var fileInfo = new FileInfo(invoicePdfPath);
            _logger.LogInformation("PDF generated at {Path} with size {Length} bytes", invoicePdfPath, fileInfo.Length);

            if (fileInfo.Length == 0)
            {
                _logger.LogError("PDF file is empty after generation!");
            }
            byte[] fileBytes = await File.ReadAllBytesAsync(invoicePdfPath);


            int createdInvoiceId = await CreateInvoiceInTripletexAsync(dto, fileBytes, "invoice.pdf", "system");

            return createdInvoiceId;
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
            _logger.LogInformation("✅ Invoice {InvoiceId} created from Order {OrderId}", invoiceId, orderId);
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

                public async Task<bool> UploadVoucherAttachmentAsync(int voucherId, byte[] fileContent, string fileName, string userId)
{
    try
    {
        // Validate inputs
        if (fileContent == null || fileContent.Length == 0)
        {
            _logger.LogWarning("No file content provided for voucher {VoucherId}", voucherId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("No filename provided for voucher {VoucherId}", voucherId);
            return false;
        }

        // Optional: Add file size validation (e.g., max 10MB)
        const int maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
        if (fileContent.Length > maxFileSizeBytes)
        {
            _logger.LogWarning("File too large for voucher {VoucherId}: {FileSize} bytes", voucherId, fileContent.Length);
            return false;
        }

        var authHeader = await _tokenService.GetAuthorizationAsync();
        var url = $"https://api-test.tripletex.tech/v2/ledger/voucher/{voucherId}/attachment";

        using var formContent = new MultipartFormDataContent();
        
        // Create file content with proper content type detection
        var fileContentContent = new ByteArrayContent(fileContent);
        
        // Try to detect content type based on file extension
        var contentType = GetContentTypeFromFileName(fileName);
        fileContentContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        // Add the file to the form with the correct field name
        formContent.Add(fileContentContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", authHeader);
        request.Content = formContent;

        _logger.LogInformation("Uploading attachment to Tripletex for Voucher ID: {VoucherId}, File: {FileName}, Size: {FileSize} bytes", 
            voucherId, fileName, fileContent.Length);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to upload attachment to voucher {VoucherId}. Status: {StatusCode}, Response: {Response}",
                voucherId, response.StatusCode, responseBody);

            // Save to log table
            var logEntry = new LogConnection
            {
                UserId = userId,
                Title = "Failed to upload attachment",
                Status = "Failed",
                Error = $"HTTP {response.StatusCode}: {responseBody}",
                FromEndpoint = "WI",
                ToEndpoint = "Tripletex",
                Date = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.LogConnections.Add(logEntry);
            await _db.SaveChangesAsync();

            return false;
        }

        _logger.LogInformation("Successfully uploaded attachment for Voucher ID {VoucherId}. Response: {Response}", 
            voucherId, responseBody);

        // Optional: Log successful upload
        var successLogEntry = new LogConnection
        {
            UserId = userId,
            Title = "Attachment uploaded successfully",
            Status = "Success",
            Error = null,
            FromEndpoint = "WI",
            ToEndpoint = "Tripletex",
            Date = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.LogConnections.Add(successLogEntry);
        await _db.SaveChangesAsync();

        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Exception occurred while uploading attachment to Tripletex for Voucher ID {VoucherId}", voucherId);

        var logEntry = new LogConnection
        {
            UserId = userId,
            Title = "Exception during attachment upload",
            Status = "Exception",
            Error = ex.ToString(),
            FromEndpoint = "WI",
            ToEndpoint = "Tripletex",
            Date = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LogConnections.Add(logEntry);
        await _db.SaveChangesAsync();

        return false;
    }
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
            // Try to load file from disk for testing (replace with real source in production)
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
