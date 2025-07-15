using System.Text.Json;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Domain.Entities;
using backend.Dtos;
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

        public async Task<IEnumerable<InvoiceDto>> GetAllWithUserAsync()
        {
            var invoices = await _invoiceRepository.GetAllWithCustomerAsync();
            return invoices.Select(invoice => new InvoiceDto
            {
                Id = invoice.Id,
                Status = invoice.Status,
                Total = invoice.Total,
                InvoiceCreated = invoice.InvoiceCreated,
                InvoiceDueDate = invoice.InvoiceDueDate,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.Customer?.Name,
                CustomerEmail = invoice.Customer?.Email
            });
        }

        public async Task<IEnumerable<Invoice>> GetAllAsync()
        {
            return await _invoiceRepository.GetAllAsync();
        }

        public async Task<Invoice> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _invoiceRepository.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                throw new KeyNotFoundException($"Invoice with id {id} not found");
            }
            return invoice;
        }

        public async Task<string> GetAuthorizationAsync()
        {
            return await _tokenService.GetAuthorizationAsync();
        }

    public async Task<List<Invoice>> GetInvoicesFromTripletexAsync()
        {
            try
        {
        var fromDate = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd");
        var toDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var url = $"https://tripletex.no/v2/invoice?invoiceDateFrom={fromDate}&invoiceDateTo={toDate}";

        var token = await _tokenService.GetTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Feil ved henting av fakturaer: {StatusCode} - {Response}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new HttpRequestException("Henting av fakturaer feilet");
        }

        var content = await response.Content.ReadAsStringAsync();
        return (await ParseTripletexInvoiceResponse(content)).ToList();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during invoice synchronization");
        throw;
    }
}

        public async Task SyncInvoicesFromTripletexAsync()
        {
            try
            {
                _logger.LogInformation("Starting invoice synchronization from Tripletex");

                var tripletexResponse = await GetInvoicesFromTripletexAsync();
                var invoicesFromApi = ParseTripletexInvoiceResponse(tripletexResponse);

                _logger.LogInformation("Found {Count} invoices from Tripletex API", invoicesFromApi.Count());

                await _invoiceRepository.BulkUpsertAsync(invoicesFromApi);

                _logger.LogInformation("Invoice synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during invoice synchronization");
                throw;
            }
        }

        private IEnumerable<Invoice> ParseTripletexInvoiceResponse(List<Invoice> tripletexResponse)
        {
            throw new NotImplementedException();
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
                        var customerTripletexId = invoiceElement.GetProperty("customer").GetProperty("id").GetInt32();
                        
                        var customer = await _customerRepository.GetByTripletexIdAsync(customerTripletexId);
                        if (customer == null)
                        {
                            _logger.LogWarning("Customer with TripletexId {TripletexId} not found, skipping invoice", customerTripletexId);
                            continue;
                        }

                        var invoice = new Invoice
                        {
                            TripletexId = invoiceElement.GetProperty("id").GetInt32(),
                            Status = invoiceElement.TryGetProperty("state", out var stateElement) ? stateElement.GetString() ?? "Unknown" : "Unknown",
                            Total = invoiceElement.TryGetProperty("amount", out var amountElement) ? amountElement.GetDouble() : 0.0,
                            InvoiceCreated = DateOnly.FromDateTime(invoiceElement.GetProperty("invoiceDate").GetDateTime()),
                            InvoiceDueDate = DateOnly.FromDateTime(invoiceElement.GetProperty("dueDate").GetDateTime()),
                            CustomerId = customer.Id
                        };

                        invoices.Add(invoice);
                    }
                }
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