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
                    var existing = _context.SaleOrder.FirstOrDefault(o => o.TripletexId == dto.Id);

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

                await _context.SaveChangesAsync();
                _logger.LogInformation("Synced {Count} sales orders successfully", orders.Count);
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

            var fromDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            var toDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");


            var url = $"https://api-test.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}&fields=*,customer(id,name)&count=1000";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);

            _logger.LogInformation("Fetching sales orders from Tripletex with URL: {Url}", url);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Tripletex API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Tripletex API Response Content: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching sales orders: {StatusCode} - {Error}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Sales order fetch failed: {response.StatusCode} - {responseContent}");
            }

            try
            {
                var result = JsonSerializer.Deserialize<SaleOrderListResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Deserialized {Count} orders from response", result?.Value?.Count ?? 0);
                
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
}