using backend.Infrastructure.Data;
using backend.Domain.interfaces;
using backend.Domain.Models;
using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerRepository> _logger;

        public CustomerRepository(AppDbContext context, ILogger<CustomerRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
{
    return await _context.Customers.ToListAsync();
}

public async Task<Customer?> GetByIdAsync(int id)
{
    return await _context.Customers.FindAsync(id);
}

public async Task<Customer?> GetByTripletexIdAsync(int tripletexId)
{
    return await _context.Customers.FirstOrDefaultAsync(c => c.TripletexId == tripletexId);
}

public async Task BulkUpsertAsync(IEnumerable<Customer> customers)
{
    foreach (var customer in customers)
    {
        var existing = await GetByTripletexIdAsync(customer.TripletexId);
        if (existing != null)
        {
            existing.Name = customer.Name;
            existing.Email = customer.Email;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            _context.Customers.Add(customer);
        }
    }

    await _context.SaveChangesAsync();
}

    }
}