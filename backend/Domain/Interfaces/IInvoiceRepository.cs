using backend.Domain.Entities;

namespace backend.Domain.interfaces
{
    public interface IInvoiceRepository
    {
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task<Invoice?> GetByIdAsync(int id);
        Task<Invoice?> GetByTripletexIdAsync(int tripletexId);
        Task<Invoice> CreateAsync(Invoice invoice);
        Task<Invoice> UpdateAsync(Invoice invoice);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int tripletexId);
        Task BulkUpsertAsync(IEnumerable<Invoice> invoices);
        Task<IEnumerable<Invoice>> GetAllWithCustomerAsync();
    }
}