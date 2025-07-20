using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using backend.Infrastructure.Data;
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
                    var existing = await _context.SaleOrder.FirstOrDefaultAsync(o => o.TripletexId == dto.Id);

                    if (existing == null)
                    {
                        var orderDate = DateOnly.Parse(dto.OrderDate);
                        var order = new SaleOrder
                        {
                            TripletexId = dto.Id,
                            OrderNumber = dto.OrderNumber,
                            Status = dto.Status,
                            OrderDate = orderDate,
                            TotalAmount = dto.TotalAmount,
                            CustomerId = dto.Customer?.Id ?? 0
                        };

                        _context.SaleOrder.Add(order);
                        _logger.LogInformation("Added new order: {OrderNumber}", dto.OrderNumber);
                    }
                    else
                    {
                        existing.Status = dto.Status;
                        existing.TotalAmount = dto.TotalAmount;
                        existing.OrderDate = DateOnly.Parse(dto.OrderDate);
                        existing.OrderNumber = dto.OrderNumber;
                        _logger.LogInformation("Updated existing order: {OrderNumber}", dto.OrderNumber);
                    }
                }

                _logger.LogInformation("Saving changes to database...");
                await _context.SaveChangesAsync();
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
            _logger.LogInformation("Authorization header obtained: {AuthHeader}", 
                authHeader != null ? authHeader.Substring(0, Math.Min(20, authHeader.Length)) + "..." : "NULL");

            // Expand date range for testing - try last 365 days
            var fromDate = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd");
            var toDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"); // Also check future orders

            // Enhanced URL with more fields and debugging parameters
            var url = $"https://api-test.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}&fields=id,number,orderDate,deliveryDate,invoicesDueDate,customer(id,name,email),currency(id,code),department(id,name),project(id,name),invoiceReference,ourReference,yourReference,deliveryAddress,invoiceAddress,comment,attention,terms,lines,discount,vatType,deliveryComment,invoiceComment&count=2000&from=0";

            _logger.LogInformation("API Request URL: {Url}", url);
            _logger.LogInformation("Date range: {FromDate} to {ToDate}", fromDate, toDate);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);
            
            // Add additional headers that might be required
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "YourApp/1.0");

            _logger.LogInformation("Fetching sales orders from Tripletex...");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Tripletex API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Response Headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
            _logger.LogInformation("Response Content Length: {Length}", responseContent?.Length ?? 0);
            
            // Log first 1000 characters of response for debugging
            var truncatedContent = responseContent?.Length > 1000 
                ? responseContent.Substring(0, 1000) + "..." 
                : responseContent;
            _logger.LogInformation("Tripletex API Response Content (truncated): {Content}", truncatedContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching sales orders: {StatusCode} - {Error}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Sales order fetch failed: {response.StatusCode} - {responseContent}");
            }

            try
            {
                // First, let's try to deserialize as a generic object to see the structure
                var jsonDocument = JsonDocument.Parse(responseContent);
                _logger.LogInformation("JSON Root Properties: {Properties}", 
                    string.Join(", ", jsonDocument.RootElement.EnumerateObject().Select(p => p.Name)));

                // Check if the response has a different structure than expected
                if (jsonDocument.RootElement.TryGetProperty("values", out var valuesElement))
                {
                    _logger.LogInformation("Found 'values' array with {Count} items", valuesElement.GetArrayLength());
                }
                else if (jsonDocument.RootElement.TryGetProperty("value", out var valueElement))
                {
                    _logger.LogInformation("Found 'value' array with {Count} items", valueElement.GetArrayLength());
                }
                else
                {
                    _logger.LogWarning("Neither 'values' nor 'value' property found in response");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Try both possible response structures
                SaleOrderListResponse result = null;
                
                try
                {
                    result = JsonSerializer.Deserialize<SaleOrderListResponse>(responseContent, options);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize with 'Value' property, trying 'Values'");
                    
                    // Try with alternative response structure
                    var alternativeResponse = JsonSerializer.Deserialize<SaleOrderListResponseAlternative>(responseContent, options);
                    result = new SaleOrderListResponse { Value = alternativeResponse?.Values ?? new List<SaleOrderDto>() };
                }

                _logger.LogInformation("Deserialized {Count} orders from response", result?.Value?.Count ?? 0);
                
                if (result?.Value?.Any() == true)
                {
                    var firstOrder = result.Value.First();
                    _logger.LogInformation("First order sample: Id={Id}, Number={Number}, Date={Date}, Status={Status}", 
                        firstOrder.Id, firstOrder.OrderNumber, firstOrder.OrderDate, firstOrder.Status);
                }

                return result?.Value ?? new List<SaleOrderDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing JSON response: {Response}", responseContent);
                throw;
            }
        }

        public async Task<List<SaleOrder>> GetAllWithUserAsync()
        {
            return await _context.SaleOrder
                .Include(o => o.Customer)
                .ToListAsync();
        }

        public async Task<SaleOrder?> GetSaleOrderByIdAsync(int id)
        {
            var order = await _context.SaleOrder
                .Include(saleOrder => saleOrder.Customer)
                .FirstOrDefaultAsync(saleOrder => saleOrder.Id == id);

            if (order == null)
                throw new KeyNotFoundException($"Sale order with ID {id} not found.");

            return order;
        }

        public async Task ImportSaleOrdersAsync()
        {
            await SyncSaleOrdersFromTripletexAsync();
        }
    }

    // Alternative response structure in case Tripletex uses 'values' instead of 'value'
    public class SaleOrderListResponseAlternative
    {
        public List<SaleOrderDto> Values { get; set; } = new();
    }
}