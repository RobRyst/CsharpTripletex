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
            var invoice = await _invoiceRepository.GetByIdAsync(id);
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
                var invoices = await ParseTripletexInvoiceResponse(content);
                
                _logger.LogInformation("Successfully fetched {Count} invoices from Tripletex", invoices.Count());
                
                return invoices.ToList();
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
                await _invoiceRepository.BulkUpsertAsync(invoicesFromApi);
                _logger.LogInformation("Invoice synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during invoice synchronization");
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