using backend.Domain.Entities;
using backend.Dtos;

namespace backend.Mappers
{
    public static class SaleOrderMapper
    {
        public static SaleOrder ToEntity(SaleOrderDto dto) => new SaleOrder
        {
            TripletexId = dto.Id,
            Number = dto.Number,
            Status = dto.Status,
            Amount = dto.Amount,
            OrderDate = DateOnly.FromDateTime(dto.OrderDate),
            CustomerId = dto.Customer?.Id ?? 0
        };

        public static SaleOrder ToEntity(TripletexCreateSaleOrder dto)
        {
            if (!DateOnly.TryParse(dto.OrderDate, out var parsedOrderDate))
            {
                throw new FormatException($"Invalid date format for OrderDate: '{dto.OrderDate}'");
            }

            return new SaleOrder
            {
                TripletexId = dto.TripletexId,
                Number = dto.Number,
                Status = dto.Status,
                Amount = dto.Amount,
                OrderDate = parsedOrderDate,
                CustomerId = dto.CustomerId
            };
        }

        public static void UpdateEntity(SaleOrder entity, SaleOrderDto dto)
        {
            entity.Status = dto.Status;
            entity.Amount = dto.Amount;
            entity.OrderDate = DateOnly.FromDateTime(dto.OrderDate);
            entity.Number = dto.Number;
            entity.CustomerId = dto.Customer?.Id ?? entity.CustomerId;
        }
    }
}
