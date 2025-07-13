using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface ICustomerRepository
    {
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer?> GetByIdAsync(int id);
        Task<Customer?> GetByTripletexIdAsync(int tripletexId);
        Task<Customer> CreateAsync(Customer customer);
        Task<Customer> UpdateAsync(Customer customer);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int tripletexId);
        Task BulkUpsertAsync(IEnumerable<Customer> customers);
    }
}