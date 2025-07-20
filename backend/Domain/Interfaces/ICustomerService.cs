using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetCustomersAsync();
        Task SyncCustomersFromTripletexAsync();
        Task<Customer> GetCustomerById(int id);
        Task<int> CreateCustomerInTripletexAsync(Customer localCustomer);
    }
}