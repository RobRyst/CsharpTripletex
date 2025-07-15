using backend.Domain.Entities;
using backend.Dtos;

namespace backend.Domain.Interfaces
{
    public interface IInvoiceService
    {
        Task<IEnumerable<InvoiceDto>> GetAllWithUserAsync();
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task<Invoice> GetInvoiceByIdAsync(int id);
        Task<string> GetAuthorizationAsync();
        Task<List<Invoice>> GetInvoicesFromTripletexAsync();
        Task SyncInvoicesFromTripletexAsync();
    }
}