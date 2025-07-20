using backend.Domain.Entities;
using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface IInvoiceRepository
    {
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task<Invoice?> GetByIdAsync(int id);
        Task<Invoice?> GetByTripletexIdAsync(int tripletexId);
        Task BulkUpsertAsync(IEnumerable<Invoice> invoices);
        Task<IEnumerable<Invoice>> GetAllWithCustomerAsync();
    }
}