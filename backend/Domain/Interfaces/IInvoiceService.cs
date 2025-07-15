using backend.Domain.Entities;
using backend.Dtos;

namespace backend.Domain.Interfaces;
public interface IInvoiceService
{
    Task<IEnumerable<InvoiceDto>> GetAllWithUserAsync();
    Task<IEnumerable<Invoice>> GetAllAsync();
    Task<Invoice> GetByIdAsync(int id);
    Task<Invoice> CreateAsync(Invoice invoice);
    Task<Invoice> UpdateAsync(int id, Invoice invoice);
    Task<bool> DeleteAsync(int id);

}