// Create this file at: backend/Repositories/CustomerRepository.cs

using backend.Infrastructure.Data;
using backend.Domain.interfaces;
using backend.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories
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
                .FirstOrDefaultAsync(c => c.TripletexId == tripletexId);
        }

        public async Task<Customer> CreateAsync(Customer customer)
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> UpdateAsync(Customer customer)
        {
            _context.Entry(customer).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task DeleteAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int tripletexId)
        {
            return await _context.Customers.AnyAsync(c => c.TripletexId == tripletexId);
        }

        public async Task BulkUpsertAsync(IEnumerable<Customer> customers)
        {
            foreach (var customer in customers)
            {
                var existingCustomer = await GetByTripletexIdAsync(customer.TripletexId);
                if (existingCustomer != null)
                {
                    // Update existing customer
                    existingCustomer.Name = customer.Name;
                    existingCustomer.Email = customer.Email;
                    _context.Entry(existingCustomer).State = EntityState.Modified;
                }
                else
                {
                    // Add new customer
                    _context.Customers.Add(customer);
                }
            }
            
            await _context.SaveChangesAsync();
        }
    }
}