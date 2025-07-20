using backend.Domain.Entities;
using backend.Domain.Models;

namespace backend.Mappers
{
    public static class CustomerMapper
{
    public static Customer ToEntity(CustomerModel model) => new Customer
    {
        TripletexId = model.TripletexId,
        Name = model.Name ?? "",
        Email = model.Email ?? "",
        OrganizationNumber = model.OrganizationNumber ?? "",
        PhoneNumber = model.PhoneNumber ?? "",
        PostalAddress = model.PostalAddress ?? "",
        AddressLine1 = model.AddressLine1 ?? "",
        PostalCode = model.PostalCode ?? "",
        City = model.City ?? "",
        Country = model.Country ?? ""
    };
}

}