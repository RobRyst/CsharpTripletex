using backend.Domain.Entities;
using backend.Domain.Interfaces;
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

        public async Task ImportSalesOrdersAsync()
        {
            var salesOrdersFromTripletex = await _saleOrderService.GetSalesOrdersFromTripletexAsync();

            foreach (var dto in salesOrdersFromTripletex)
            {
                var salesOrder = new SaleOrder
                {
                    TripletexId = dto.Id,
                    OrderNumber = dto.OrderNumber,
                    Status = dto.Status,
                    TotalAmount = dto.TotalAmount,
                    OrderDate = DateOnly.Parse(dto.OrderDate),
                    CustomerId = dto.Customer?.Id ?? 0,
                };

                _db.SaleOrder.Add(salesOrder);
            }

            await _db.SaveChangesAsync();
        }

        public async Task SyncSalesOrdersFromTripletexAsync()
        {
            await _saleOrderService.SyncSalesOrdersFromTripletexAsync();
        }
        public async Task<List<SalesOrderDto>> GetSalesOrdersFromTripletexAsync()
        {
            return await _saleOrderService.GetSalesOrdersFromTripletexAsync();
        }
    }
}