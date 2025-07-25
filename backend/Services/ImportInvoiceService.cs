using backend.Domain.Models;
using backend.Dtos;
using backend.Infrastructure.Data;
using backend.Domain.Entities;

namespace backend.Services
{
    public class InvoiceImportService
    {
        private readonly TripleTexService _client;
        private readonly AppDbContext _db;

        public InvoiceImportService(TripleTexService client, AppDbContext db)
        {
            _client = client;
            _db = db;
        }

        public async Task ImportInvoicesAsync()
        {
            var invoicesFromTripletexApi = await _client.GetInvoicesAsync();

            foreach (var dto in invoicesFromTripletexApi)
            {
                var invoice = new Invoice
                {
                    TripletexId = dto.TripletexId,
                    Total = dto.Total ?? 0,
                    InvoiceCreated = dto.InvoiceCreated,
                    InvoiceDueDate = dto.InvoiceDueDate,
                    CustomerId = dto.CustomerId,
                    VoucherId = dto.VoucherId
                };

                _db.Invoices.Add(invoice);
            }

            await _db.SaveChangesAsync();
        }
    }
}

