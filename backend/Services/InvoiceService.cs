using System.Text.Json;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using System.Text;
using backend.Domain.Entities;
using backend.Domain.Models;
using backend.Mappers;

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

public async Task<IEnumerable<InvoiceModel>> GetAllWithCustomerAsync()
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

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Error fetching invoices: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Invoice fetch failed: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var invoiceEntities = await ParseTripletexInvoiceResponse(content);
        
        var invoiceModels = invoiceEntities.Select(InvoiceMapper.ToModel).ToList();
        
        _logger.LogInformation("Successfully fetched {Count} invoices from Tripletex", invoiceModels.Count);
        _logger.LogDebug("Tripletex Raw Invoice JSON: {Json}", content);
        
        return invoiceModels;
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
        
        public async Task<int> CreateInvoiceInTripletexAsync(InvoiceModel invoice)
{
    try
    {
        var authHeader = await _tokenService.GetAuthorizationAsync();
        var tripletexInvoiceDto = new TripletexInvoiceCreateDto
        {
            Customer = new TripletexCustomerRefDto
            {
                Id = invoice.CustomerTripletexId
            },
            InvoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
            DueDate = invoice.DueDate.ToString("yyyy-MM-dd"),
            Currency = invoice.Currency ?? "NOK",
            Status = "DRAFT"
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(tripletexInvoiceDto, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Creating invoice in Tripletex with data: {Json}", json);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/invoice");
        request.Headers.Add("Authorization", authHeader);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Tripletex response: {StatusCode} - {Response}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error creating invoice in Tripletex: {StatusCode} - {Error}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Invoice creation failed: {response.StatusCode} - {responseBody}");
        }

        var result = JsonSerializer.Deserialize<TripletexResponseDto>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result?.Value?.Id ?? 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating invoice in Tripletex");
        throw;
    }
}

        private async Task<IEnumerable<Invoice>> ParseTripletexInvoiceResponse(string jsonResponse)
        {
            var invoices = new List<Invoice>();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("values", out var valuesElement))
                {
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

                        var invoice = new Invoice
                        {
                            TripletexId = invoiceElement.GetProperty("id").GetInt32(),
                            Status = invoiceElement.TryGetProperty("state", out var stateElement) ?
                                stateElement.GetString() ?? "Unknown" : "Unknown",
                            Total = invoiceElement.TryGetProperty("amount", out var amountElement) ?
                                amountElement.GetDouble() : 0.0,
                            InvoiceCreated = invoiceElement.TryGetProperty("invoiceDate", out var invoiceDateElement) ?
                                DateOnly.FromDateTime(invoiceDateElement.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow),
                            InvoiceDueDate = invoiceElement.TryGetProperty("dueDate", out var dueDateElement) ?
                                DateOnly.FromDateTime(dueDateElement.GetDateTime()) : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                            CustomerId = customer.Id
                        };

                        invoices.Add(invoice);
                    }
                }
                else
                {
                    _logger.LogWarning("No 'values' property found in Tripletex response");
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
    }
}