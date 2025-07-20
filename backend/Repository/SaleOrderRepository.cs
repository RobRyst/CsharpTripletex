using backend.Domain.Entities;
using backend.Domain.interfaces;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository
{
    public class SaleOrderRepository : ISaleOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SaleOrderRepository> _logger;

        public SaleOrderRepository(AppDbContext context, ILogger<SaleOrderRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<SaleOrder>> GetAllAsync()
        {
            return await _context.SaleOrder.Include(so => so.Customer).ToListAsync();
        }

        public async Task<SaleOrder?> GetByTripletexIdAsync(int tripletexId)
        {
            return await _context.SaleOrder
                .Include(so => so.Customer)
                .FirstOrDefaultAsync(so => so.TripletexId == tripletexId);
        }

        public async Task BulkUpsertAsync(IEnumerable<SaleOrder> saleOrders)
        {
            foreach (var saleOrder in saleOrders)
            {
                var existing = await GetByTripletexIdAsync(saleOrder.TripletexId);
                if (existing != null)
                {
                    existing.OrderNumber = saleOrder.OrderNumber;
                    existing.Status = saleOrder.Status;
                    existing.TotalAmount = saleOrder.TotalAmount;
                    existing.OrderDate = saleOrder.OrderDate;
                    existing.CustomerId = saleOrder.CustomerId;
                    _context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    _context.SaleOrder.Add(saleOrder);
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
