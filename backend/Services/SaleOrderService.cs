using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Domain.Interfaces;
using backend.Dtos;
using backend.Infrastructure.Data;
using backend.Mappers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http.Headers;

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
                    var existing = await _context.Saleorders.FirstOrDefaultAsync(o => o.TripletexId == dto.Id);

                    if (existing == null)
                    {
                        var order = SaleOrderMapper.ToEntity(dto);
                        _context.Saleorders.Add(order);
                        _logger.LogInformation("New order added: {OrderNumber}", dto.Number);
                    }
                    else
                    {
                        SaleOrderMapper.UpdateEntity(existing, dto);
                        _logger.LogInformation("Existing order updated: {OrderNumber}", dto.Number);
                    }
                }

                _logger.LogInformation("Saving changes to database");
                await _context.SaveChangesAsync();
                await Task.Delay(2000);
                _logger.LogInformation("Save successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during order sync");
                throw;
            }
        }

        public async Task<List<SaleOrderDto>> GetSaleOrdersFromTripletexAsync()
        {
            var authHeader = await _tokenService.GetAuthorizationAsync();
            var fromDate = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd");
            var toDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            var url = $"https://api-test.tripletex.tech/v2/order?orderDateFrom={fromDate}&orderDateTo={toDate}&fields=id,number,status,orderDate,customer(id,name,email)";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", authHeader);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "YourApp/1.0");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error getting sale orders: {StatusCode} - {Error}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed getting sale order: {response.StatusCode} - {responseContent}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            try
            {
                SaleOrderListResponse result;
                try
                {
                    result = JsonSerializer.Deserialize<SaleOrderListResponse>(responseContent, options);
                }
                catch (JsonException)
                {
                    var alt = JsonSerializer.Deserialize<SaleOrderListResponseAlternative>(responseContent, options);
                    result = new SaleOrderListResponse { Value = alt?.Values ?? new() };
                }

                return result?.Value ?? new();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing JSON from Tripletex");
                throw;
            }
        }

        public async Task<List<SaleOrderDto>> GetAllWithUserAsync()
        {
            return await _context.Saleorders
                .Include(o => o.Customer)
                .Select(o => new SaleOrderDto
                {
                    Id = o.Id,
                    Number = o.Number,
                    Status = o.Status,
                    OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue),
                    Amount = o.Amount,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.Name
                })
                .ToListAsync();
        }

        public async Task<SaleOrderDto?> GetSaleOrderByIdAsync(int id)
        {
            var order = await _context.Saleorders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new KeyNotFoundException($"Sale order with ID {id} not found.");

            return new SaleOrderDto
            {
                Id = order.Id,
                Number = order.Number,
                Status = order.Status,
                OrderDate = order.OrderDate.ToDateTime(TimeOnly.MinValue),
                Amount = order.Amount,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.Name
            };
        }

        public async Task<SaleOrderDto> CreateSaleOrderAsync(TripletexCreateSaleOrder dto)
{
    _logger.LogInformation("Received DTO with CustomerId: {CustomerId}", dto.CustomerId);

    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId);
    if (customer == null || customer.TripletexId == null || customer.TripletexId == 0)
        throw new InvalidOperationException($"Customer with TripletexId {dto.CustomerId} not found or missing TripletexId.");

    if (!DateOnly.TryParse(dto.OrderDate, out var parsedOrderDate))
        throw new FormatException($"Invalid OrderDate format: '{dto.OrderDate}'");

    var orderPayload = new {
        orderDate = dto.OrderDate,
        deliveryDate = dto.OrderDate,
        customer = new { id = customer.TripletexId },
        invoicesDueIn = 14,
        invoicesDueInType = "DAYS",
        isShowOpenPostsOnInvoices = false,
        orderLineSorting = "PRODUCT",
        isPrioritizeAmountsIncludingVat = false,
        orderLines = new[] {
            new {
                product = new { id = 69691388 },
                description = "Consulting services",
                count = 1,
                unitPriceExcludingVatCurrency = dto.Amount,
                vatType = new { id = 3 }
            }
        }
    };

    var authHeader = await _tokenService.GetAuthorizationAsync();

    var request = new HttpRequestMessage(HttpMethod.Post, "https://api-test.tripletex.tech/v2/order");
    request.Headers.Add("Authorization", authHeader);
    request.Content = JsonContent.Create(orderPayload);

    var response = await _httpClient.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("Order creation failed: {StatusCode} - {Content}", response.StatusCode, content);
        throw new HttpRequestException($"Failed to create sale order: {response.StatusCode}");
    }

    var orderResult = JsonSerializer.Deserialize<TripletexSaleOrderResponse>(content, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    var orderId = orderResult?.Value?.Id;
    if (orderId == null)
        throw new Exception("Order ID missing in Tripletex response.");

    var order = new SaleOrder
    {
        TripletexId = (int)orderId,
        Number = dto.Number,
        Status = "Created",
        Amount = dto.Amount,
        OrderDate = parsedOrderDate,
        CustomerId = customer.Id
    };

    _context.Saleorders.Add(order);
    await _context.SaveChangesAsync();
    await Task.Delay(2000);

    return new SaleOrderDto
    {
        Id = order.Id,
        Number = order.Number,
        Status = order.Status,
        OrderDate = order.OrderDate.ToDateTime(TimeOnly.MinValue),
        Amount = order.Amount,
        CustomerId = order.CustomerId,
        CustomerName = customer.Name
    };
}

        public async Task ImportSaleOrdersAsync()
        {
            await SyncSaleOrdersFromTripletexAsync();
        }
    }

    public class SaleOrderListResponseAlternative
    {
        public List<SaleOrderDto> Values { get; set; } = new();
    }
}
