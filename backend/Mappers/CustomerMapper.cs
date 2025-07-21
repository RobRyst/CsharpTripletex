using backend.Domain.Entities;
using backend.Domain.Models;
using backend.Dtos;

namespace backend.Mappers
{
    public static class CustomerMapper
    {
        public static CustomerModel ToModel(CustomerDto dto) => new CustomerModel
        {
            Id = dto.Id, // This will be 0 if coming from Tripletex only
            TripletexId = dto.TripletexId,
            Name = dto.Name ?? string.Empty,
            Email = dto.Email ?? string.Empty,
            OrganizationNumber = dto.OrganizationNumber ?? string.Empty,
            PhoneNumber = dto.PhoneNumber ?? string.Empty,
            PostalAddress = dto.PostalAddress ?? string.Empty,
            AddressLine1 = dto.AddressLine1 ?? string.Empty,
            PostalCode = dto.PostalCode ?? string.Empty,
            City = dto.City ?? string.Empty,
            Country = dto.Country ?? string.Empty
        };

        public static CustomerModel ToModel(Customer entity) => new CustomerModel
        {
            Id = entity.Id,
            TripletexId = entity.TripletexId,
            Name = entity.Name ?? string.Empty,
            Email = entity.Email ?? string.Empty,
            OrganizationNumber = entity.OrganizationNumber ?? string.Empty,
            PhoneNumber = entity.PhoneNumber ?? string.Empty,
            PostalAddress = entity.PostalAddress ?? string.Empty,
            AddressLine1 = entity.AddressLine1 ?? string.Empty,
            PostalCode = entity.PostalCode ?? string.Empty,
            City = entity.City ?? string.Empty,
            Country = entity.Country ?? string.Empty
        };

        public static Customer ToEntity(CustomerModel model) => new Customer
        {
            Id = model.Id,
            TripletexId = model.TripletexId,
            Name = model.Name,
            Email = model.Email,
            OrganizationNumber = model.OrganizationNumber,
            PhoneNumber = model.PhoneNumber,
            PostalAddress = model.PostalAddress,
            AddressLine1 = model.AddressLine1,
            PostalCode = model.PostalCode,
            City = model.City,
            Country = model.Country
        };

        public static void UpdateEntity(Customer entity, CustomerDto dto)
        {
            entity.TripletexId = dto.TripletexId;
            entity.Name = dto.Name ?? entity.Name;
            entity.Email = dto.Email ?? entity.Email;
            entity.OrganizationNumber = dto.OrganizationNumber ?? entity.OrganizationNumber;
            entity.PhoneNumber = dto.PhoneNumber ?? entity.PhoneNumber;
            entity.PostalAddress = dto.PostalAddress ?? entity.PostalAddress;
            entity.AddressLine1 = dto.AddressLine1 ?? entity.AddressLine1;
            entity.PostalCode = dto.PostalCode ?? entity.PostalCode;
            entity.City = dto.City ?? entity.City;
            entity.Country = dto.Country ?? entity.Country;
        }
    }
}
