using backend.Infrastructure.Data;
using backend.Domain.interfaces;
using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceRepository> _logger;

        public InvoiceRepository(AppDbContext context, ILogger<InvoiceRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Invoice>> GetAllAsync()
        {
            return await _context.Invoices.ToListAsync();
        }

        public async Task<IEnumerable<Invoice>> GetAllWithCustomerAsync()
        {
            return await _context.Invoices
                .Include(invoice => invoice.Customer)
                .ToListAsync();
        }

        public async Task<Invoice?> GetByIdAsync(int id)
        {
            return await _context.Invoices
                .Include(invoice => invoice.Customer)
                .FirstOrDefaultAsync(invoice => invoice.Id == id);
        }

        public async Task<Invoice?> GetByTripletexIdAsync(int tripletexId)
        {
            return await _context.Invoices
                .Include(invoice => invoice.Customer)
                .FirstOrDefaultAsync(invoice => invoice.TripletexId == tripletexId);
        }

        public async Task<Invoice> CreateAsync(Invoice invoice)
        {
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<Invoice> UpdateAsync(Invoice invoice)
        {
            _context.Entry(invoice).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task DeleteAsync(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int tripletexId)
        {
            return await _context.Invoices.AnyAsync(invoice => invoice.TripletexId == tripletexId);
        }

        public async Task BulkUpsertAsync(IEnumerable<Invoice> invoices)
        {
            foreach (var invoice in invoices)
            {
                var existingInvoice = await GetByTripletexIdAsync(invoice.TripletexId);
                if (existingInvoice != null)
                {
                    existingInvoice.Status = invoice.Status;
                    existingInvoice.Total = invoice.Total;
                    existingInvoice.InvoiceCreated = invoice.InvoiceCreated;
                    existingInvoice.InvoiceDueDate = invoice.InvoiceDueDate;
                    existingInvoice.CustomerId = invoice.CustomerId;
                    _context.Entry(existingInvoice).State = EntityState.Modified;
                }
                else
                {
                    _context.Invoices.Add(invoice);
                }
            }
            
            await _context.SaveChangesAsync();
        }
    }
}