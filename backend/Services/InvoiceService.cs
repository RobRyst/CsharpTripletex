using System.Text.Json;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using System.Text;
using backend.Domain.Entities;
using backend.Domain.Models;
using backend.Mappers;
using System.Net.Http.Headers;

namespace backend.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            HttpClient httpClient,
            ITokenService tokenService,
            IInvoiceRepository invoiceRepository,
            ICustomerRepository customerRepository,
            ILogger<InvoiceService> logger)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _invoiceRepository = invoiceRepository;
            _customerRepository = customerRepository;
            _logger = logger;
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

    public async Task<int> CreateInvoiceInTripletexAsync(TripletexInvoiceCreateDto dto)
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

        // Create invoice directly with embedded orders and order lines
        var invoicePayload = new
        {
            customer = new { id = customer.TripletexId },
            invoiceDate = invoiceModel.InvoiceDate.ToString("yyyy-MM-dd"),
            invoiceDueDate = invoiceModel.InvoiceDueDate.ToString("yyyy-MM-dd"),
            currency = new { id = 1 }, // NOK
            // Note: state and sendMethodDescription are read-only fields set by Tripletex
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

        var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/invoice");
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
        if (invoiceId == 0)
        {
            throw new Exception("Invoice ID could not be extracted from Tripletex response.");
        }

        _logger.LogInformation("Successfully created invoice with ID: {InvoiceId}", invoiceId);

        // IMPORTANT: After creating the invoice, immediately fetch it back to see what Tripletex actually stored
        await LogCreatedInvoiceDetails(invoiceId, authHeader);

        return invoiceId;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating invoice in Tripletex");
        throw;
    }
}

// Helper method to debug what was actually created
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

                // Only log what's actually available
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
