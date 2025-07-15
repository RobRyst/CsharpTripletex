using backend.Domain.Models;
using backend.Infrastructure.Data;

public class CustomerImportService
{
    private readonly TripleTexService _client;
    private readonly AppDbContext _db;

    public CustomerImportService(TripleTexService client, AppDbContext db)
    {
        _client = client;
        _db = db;
    }

    public async Task ImportCustomersAsync()
    {
        var customersFromTripletexApi = await _client.GetCustomersAsync();

        foreach (var dto in customersFromTripletexApi)
        {
            var customer = new Customer
            {
                Name = dto.Name,
                Email = dto.Email
            };

            _db.Customers.Add(customer);
        }

        await _db.SaveChangesAsync();
    }
}
