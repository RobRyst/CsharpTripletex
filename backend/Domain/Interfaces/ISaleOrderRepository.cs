using backend.Domain.Entities;

namespace backend.Domain.interfaces
{
    public interface ISaleOrderRepository
    {
        Task<IEnumerable<SaleOrder>> GetAllAsync();
        Task<SaleOrder?> GetByTripletexIdAsync(int tripletexId);
        Task BulkUpsertAsync(IEnumerable<SaleOrder> saleOrders);
    }
}
