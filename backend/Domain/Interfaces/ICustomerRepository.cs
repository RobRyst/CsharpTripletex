using backend.Domain.Entities;

namespace backend.Domain.interfaces
{
    public interface ICustomerRepository
    {
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer?> GetByIdAsync(int id);
        Task<Customer?> GetByTripletexIdAsync(int tripletexId);
        Task BulkUpsertAsync(IEnumerable<Customer> customers);
    }
}
