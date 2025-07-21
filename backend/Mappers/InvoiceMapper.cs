using backend.Domain.Entities;
using backend.Domain.Models;
using backend.Dtos;

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
        public static InvoiceModel FromTripletexDto(TripletexInvoiceCreateDto dto, int localCustomerId)
        {
            var parseSuccess1 = DateOnly.TryParse(dto.InvoiceDate, out var createdDate);
            var parseSuccess2 = DateOnly.TryParse(dto.InvoiceDueDate, out var dueDate);

            if (!parseSuccess1 || !parseSuccess2)
                throw new FormatException("Invalid date format for invoice");

            return new InvoiceModel
            {
                CustomerId = localCustomerId,
                Currency = dto.Currency?.Code ?? "NOK",
                InvoiceCreated = createdDate,
                InvoiceDueDate = dueDate,
                Total = dto.InvoiceLines?.FirstOrDefault()?.UnitPrice ?? 0
            };
        }
    }
}
