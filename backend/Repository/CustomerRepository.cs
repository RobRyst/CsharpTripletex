using backend.Infrastructure.Data;
using backend.Domain.interfaces;
using backend.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Respository
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
            return await _context.Customers
                .FirstOrDefaultAsync(customer => customer.TripletexId == tripletexId);
        }

        public async Task BulkUpsertAsync(IEnumerable<Customer> customers)
        {
            foreach (var customer in customers)
            {
                var existingCustomer = await GetByTripletexIdAsync(customer.TripletexId);
                if (existingCustomer != null)
                {
                    existingCustomer.Name = customer.Name;
                    existingCustomer.Email = customer.Email;
                    _context.Entry(existingCustomer).State = EntityState.Modified;
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