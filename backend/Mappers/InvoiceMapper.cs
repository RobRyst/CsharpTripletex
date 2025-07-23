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
            TripletexId = entity.TripletexId ?? 0,
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
            Console.WriteLine($"DEBUG: dto.InvoiceDate = {dto.InvoiceDate}");
            Console.WriteLine($"DEBUG: dto.InvoiceDueDate = {dto.InvoiceDueDate}");
            if (!DateOnly.TryParseExact(dto.InvoiceDate, "yyyy-MM-dd", out var invoiceDate))
            {
                throw new FormatException($"Invalid date format for invoiceDate: {dto.InvoiceDate}");
            }

            if (!DateOnly.TryParseExact(dto.InvoiceDueDate, "yyyy-MM-dd", out var invoiceDueDate))
            {
                throw new FormatException($"Invalid date format for invoiceDueDate: {dto.InvoiceDueDate}");
            }

            return new InvoiceModel
            {
                CustomerId = localCustomerId,
                Currency = "NOK",
                InvoiceCreated = invoiceDate,
                InvoiceDueDate = invoiceDueDate,
                Total = dto.Orders?.FirstOrDefault()?.OrderLines?.FirstOrDefault()?.UnitPriceExcludingVatCurrency ?? 0
            };
        }
    }
}
