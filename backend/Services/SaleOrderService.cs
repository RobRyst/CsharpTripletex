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
                    }
                    else
                    {
                        existing.Status = dto.Status;
                        existing.TotalAmount = dto.TotalAmount;
                        existing.OrderDate = DateOnly.Parse(dto.OrderDate);
                        existing.OrderNumber = dto.OrderNumber;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Synced sales orders successfully");
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

            var url = $"https://api-test.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);

            _logger.LogInformation("Fetching sales orders from Tripletex...");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error fetching sales orders: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Sales order fetch failed: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SaleOrderListResponse>(json);

            return result?.Value ?? new();
        }

        public async Task<List<SaleOrder>> GetAllWithUserAsync()
{
    return await _context.SaleOrder
        .Include(o => o.Customer) // Assuming SaleOrder has a navigation property `Customer`
        .ToListAsync();
}

public async Task<SaleOrder?> GetSaleOrderByIdAsync(int id)
{
    var order = await _context.SaleOrder
        .Include(o => o.Customer)
        .FirstOrDefaultAsync(o => o.Id == id);

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