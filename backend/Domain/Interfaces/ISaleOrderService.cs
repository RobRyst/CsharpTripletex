using backend.Dtos;

namespace backend.Domain.Interfaces
{
    public interface ISaleOrderService
    {
        Task SyncSaleOrdersFromTripletexAsync();
        Task<List<SaleOrderDto>> GetSaleOrdersFromTripletexAsync();
        Task ImportSaleOrdersAsync();
    }
}