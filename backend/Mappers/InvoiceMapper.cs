using backend.Domain.Entities;
using backend.Domain.Models;

namespace backend.Mappers
{
    public static class InvoiceMapper
    {
        public static Invoice ToEntity(InvoiceModel model) => new Invoice
        {
            Id = model.Id,
            TripletexId = model.TripletexId,
            Total = model.Total,
            InvoiceCreated = model.InvoiceCreated,
            InvoiceDueDate = model.InvoiceDueDate,
            InvoiceDate = model.InvoiceDate,
            DueDate = model.DueDate,
            Currency = model.Currency ?? "NOK",
            CustomerId = model.CustomerId,
            CustomerTripletexId = model.CustomerTripletexId
        };

        public static InvoiceModel ToModel(Invoice entity) => new InvoiceModel
        {
            Id = entity.Id,
            TripletexId = entity.TripletexId,
            Total = entity.Total,
            InvoiceCreated = entity.InvoiceCreated,
            InvoiceDueDate = entity.InvoiceDueDate,
            Currency = entity.Currency,
            CustomerId = entity.CustomerId ?? 0,
            Customer = entity.Customer != null
                ? CustomerMapper.ToModel(entity.Customer)
                : null
        };
    }
}
