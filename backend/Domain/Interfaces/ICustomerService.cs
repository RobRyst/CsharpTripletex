using backend.Domain.Models;

namespace backend.Domain.interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetCustomersFromDatabaseAsync();
        Task<string> GetCustomersAsync();
        Task SyncCustomersFromTripletexAsync();
    }
}