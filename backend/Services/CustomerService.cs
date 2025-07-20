using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Domain.Models;
using backend.Dtos;
using backend.Mappers;

namespace backend.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
                HttpClient httpClient,
                ITokenService tokenService,
                ICustomerRepository customerRepository,
                ILogger<CustomerService> logger)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<Customer>> GetCustomersFromDatabaseAsync()
        {
            return await _customerRepository.GetAllAsync();
        }


        public async Task<IEnumerable<CustomerModel>> GetCustomersAsync()
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api-test.tripletex.tech/v2/customer");
            request.Headers.Add("Authorization", authHeader);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Feil ved henting av kunder: {Status} - {Error}", response.StatusCode, error);
                throw new HttpRequestException("Henting av kunder feilet");
            }

            var content = await response.Content.ReadAsStringAsync();
            return ParseTripletexResponse(content);
        }

        public async Task<CustomerModel> GetCustomerById(int id)
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api-test.tripletex.tech/v2/customer/{id}");

            request.Headers.Add("Authorization", authHeader);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Feil ved henting av kunde med ID {Id}", id);
                throw new HttpRequestException("Henting av kunde feilet");
            }

            var json = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(json);

            var root = jsonDoc.RootElement;

            var customer = new CustomerModel
            {
                TripletexId = root.GetProperty("id").GetInt32(),
                Name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null,
                Email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null
            };

            return customer;
        }
        public async Task SyncCustomersFromTripletexAsync()
{
    try
    {
        _logger.LogInformation("Starting customer synchronization from Tripletex");

        var customersFromApi = await GetCustomersAsync();

        _logger.LogInformation("Found {Count} customers from Tripletex API", customersFromApi.Count());

        var customerEntities = customersFromApi.Select(CustomerMapper.ToEntity).ToList();
        await _customerRepository.BulkUpsertAsync(customerEntities);

        _logger.LogInformation("Customer synchronization completed successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during customer synchronization");
        throw;
    }
}

        private IEnumerable<CustomerModel> ParseTripletexResponse(string jsonResponse)
        {
            var customers = new List<CustomerModel>();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("values", out var valuesElement))
                {
                    foreach (var customerElement in valuesElement.EnumerateArray())
                    {
                        var customer = new CustomerModel
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
        
        public async Task<int> CreateCustomerInTripletexAsync(CustomerModel localCustomer)
{
    try
    {
var tripletexDto = new TripletexCustomerCreateDto
{
    Name = localCustomer.Name!,
    Email = localCustomer.Email,
    OrganizationNumber = localCustomer.OrganizationNumber,
    PhoneNumber = localCustomer.PhoneNumber,
    IsCustomer = true,
        PostalAddress = new TripletexAddressDto
    {
        AddressLine1 = localCustomer.AddressLine1,
        PostalCode = localCustomer.PostalCode,
        City = localCustomer.City,
        Country = new TripletexCountryDto { Id = 160 }
    }

};

        var url = "https://api-test.tripletex.tech/v2/customer";
        var token = await _tokenService.GetAuthorizationAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json = JsonSerializer.Serialize(tripletexDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create customer in Tripletex: {StatusCode} - {Content}", response.StatusCode, content);
            throw new HttpRequestException($"Tripletex error: {response.StatusCode}");
        }
        

        var doc = JsonDocument.Parse(content);
        var id = doc.RootElement.GetProperty("value").GetProperty("id").GetInt32();

        _logger.LogInformation("Created customer in Tripletex with ID {TripletexId}", id);

        return id;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating customer in Tripletex");
        throw;
    }
}

    }
}