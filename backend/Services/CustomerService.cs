using System.Text.Json;
using backend.Domain.interfaces;
using backend.Domain.Models;

namespace backend.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ITokenService _tokenService;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            ITokenService tokenService, 
            ICustomerRepository customerRepository,
            ILogger<CustomerService> logger)
        {
            _tokenService = tokenService;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<Customer>> GetCustomersFromDatabaseAsync()
        {
            return await _customerRepository.GetAllAsync();
        }

        public async Task SyncCustomersFromTripletexAsync()
        {
            try
            {
                _logger.LogInformation("Starting customer synchronization from Tripletex");
                
                var tripletexResponse = await _tokenService.GetCustomersAsync();
                var customersFromApi = ParseTripletexResponse(tripletexResponse);
                
                _logger.LogInformation("Found {Count} customers from Tripletex API", customersFromApi.Count());
                
                await _customerRepository.BulkUpsertAsync(customersFromApi);
                
                _logger.LogInformation("Customer synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during customer synchronization");
                throw;
            }
        }

        private IEnumerable<Customer> ParseTripletexResponse(string jsonResponse)
        {
            var customers = new List<Customer>();
            
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                
                if (jsonDoc.RootElement.TryGetProperty("values", out var valuesElement))
                {
                    foreach (var customerElement in valuesElement.EnumerateArray())
                    {
                        var customer = new Customer
                        {
                            TripletexId = customerElement.GetProperty("id").GetInt32(),
                            Name = customerElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null,
                            Email = customerElement.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null
                        };
                        
                        customers.Add(customer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Tripletex response");
                throw;
            }
            
            return customers;
        }
    }
}