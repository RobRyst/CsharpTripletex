using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetCustomersAsync();
        Task<string> GetCustomerById();
        Task SyncCustomersFromTripletexAsync();
    }
}