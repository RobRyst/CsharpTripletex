using backend.Domain.Entities;
using backend.Domain.Interfaces;
using backend.Domain.Models;
using backend.Dtos;
using backend.Infrastructure.Data;
using backend.Services;

namespace backend.Services
{
    public class ImportSaleOrderService : ISaleOrderService
    {
        private readonly SaleOrderService _saleOrderService;
        private readonly AppDbContext _db;

        public ImportSaleOrderService(SaleOrderService saleOrderService, AppDbContext db)
        {
            _saleOrderService = saleOrderService;
            _db = db;
        }

        public async Task ImportSaleOrdersAsync()
        {
            var saleOrdersFromTripletex = await _saleOrderService.GetSaleOrdersFromTripletexAsync();

            foreach (var dto in saleOrdersFromTripletex)
            {
                var saleOrder = new SaleOrder
                {
                    TripletexId = dto.Id,
                    OrderNumber = dto.OrderNumber,
                    Status = dto.Status,
                    TotalAmount = dto.TotalAmount,
                    OrderDate = DateOnly.Parse(dto.OrderDate),
                    CustomerId = dto.Customer?.Id ?? 0,
                };

                _db.Saleorders.Add(saleOrder);
            }

            await _db.SaveChangesAsync();
        }

        public async Task SyncSaleOrdersFromTripletexAsync()
        {
            await _saleOrderService.SyncSaleOrdersFromTripletexAsync();
        }
        public async Task<List<SaleOrderDto>> GetSaleOrdersFromTripletexAsync()
        {
            return await _saleOrderService.GetSaleOrdersFromTripletexAsync();
        }
    }
}