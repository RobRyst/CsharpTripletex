using backend.Domain.Models;
using backend.Dtos;

namespace backend.Mappers
{
 public static class CustomerDtoMapper
{
    public static CustomerModel ToModel(CustomerDto dto) => new CustomerModel
    {
        TripletexId = dto.TripletexId,
        Name = dto.Name,
        Email = dto.Email,
        OrganizationNumber = dto.OrganizationNumber,
        PhoneNumber = dto.PhoneNumber,
        PostalAddress = dto.PostalAddress,
        AddressLine1 = dto.AddressLine1,
        PostalCode = dto.PostalCode,
        City = dto.City,
        Country = dto.Country
    };
}   
}
