using backend.Infrastructure.Data;
using backend.Domain.interfaces;
using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using backend.Domain.Models;

namespace backend.Repository
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
            return await _context.Invoices.Include(i => i.Customer).ToListAsync();
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

        public async Task BulkUpsertAsync(IEnumerable<Invoice> invoices)
        {
            foreach (var invoice in invoices)
            {
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(dbInvoice => dbInvoice.TripletexId == invoice.TripletexId);

                if (existingInvoice != null)
                {
                    existingInvoice.Status = invoice.Status;
                    existingInvoice.Total = invoice.Total;
                    existingInvoice.InvoiceCreated = invoice.InvoiceCreated;
                    existingInvoice.InvoiceDueDate = invoice.InvoiceDueDate;
                    existingInvoice.CustomerId = invoice.CustomerId;
                    _context.Invoices.Update(existingInvoice);
                }
                else
                {
                    await _context.Invoices.AddAsync(invoice);
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}