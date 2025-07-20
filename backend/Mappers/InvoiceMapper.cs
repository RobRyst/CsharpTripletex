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
            Status = model.Status ?? "Unknown",
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
            Status = entity.Status,
            Total = entity.Total,
            InvoiceCreated = entity.InvoiceCreated,
            InvoiceDueDate = entity.InvoiceDueDate,
            Currency = entity.Currency,
            CustomerId = entity.CustomerId ?? 0,
            Customer = entity.Customer != null ? new CustomerModel
            {
                Id = entity.Customer.Id,
                TripletexId = entity.Customer.TripletexId,
                Name = entity.Customer.Name,
                Email = entity.Customer.Email,
                OrganizationNumber = entity.Customer.OrganizationNumber,
                PhoneNumber = entity.Customer.PhoneNumber,
                PostalAddress = entity.Customer.PostalAddress,
                AddressLine1 = entity.Customer.AddressLine1,
                PostalCode = entity.Customer.PostalCode,
                City = entity.Customer.City,
                Country = entity.Customer.Country
            } : null
        };
    }
}
