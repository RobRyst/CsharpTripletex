using backend.Domain.Entities;
using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerModel?>> GetCustomersAsync();
        Task SyncCustomersFromTripletexAsync();
        Task<CustomerModel?> GetCustomerById(int id);
        Task<int> CreateCustomerInTripletexAsync(CustomerModel? customer);
        Task<IEnumerable<Customer>> GetCustomersFromDatabaseAsync();
    }
}