// IInvoiceService.cs
using backend.Domain.Models;
using backend.Dtos;

namespace backend.Domain.Interfaces
{
    public interface IInvoiceService
    {
        Task<IEnumerable<InvoiceModel>> GetAllAsync();
        Task<IEnumerable<InvoiceModel>> GetAllInvoicesAsync();
        Task<InvoiceModel> GetInvoiceByIdAsync(int id);
        Task<List<InvoiceModel>> GetInvoicesFromTripletexAsync();
        Task SyncInvoicesFromTripletexAsync();
        Task<int> CreateInvoiceInTripletexAsync(TripletexInvoiceCreateDto dto, byte[] fileBytes, string fileName, string userId);
        Task<int> CreateInvoiceInTripletexAsync(TripletexInvoiceCreateDto dto);

    }
}
