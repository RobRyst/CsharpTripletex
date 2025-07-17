using backend.Dtos;

namespace backend.Domain.Interfaces
{
    public interface ISaleOrderService
    {
        Task SyncSalesOrdersFromTripletexAsync();
        Task<List<SalesOrderDto>> GetSalesOrdersFromTripletexAsync();
        Task ImportSalesOrdersAsync();
    }
}