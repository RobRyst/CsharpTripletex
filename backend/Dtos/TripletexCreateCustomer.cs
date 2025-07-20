using System.Text.Json.Serialization;

namespace backend.Dtos
{
    public class TripletexCountryDto
{
    public int Id { get; set; }
}

public class TripletexAddressDto
{
    public string AddressLine1 { get; set; } = null!;

    public string PostalCode { get; set; } = null!;

    public string City { get; set; } = null!;

    public TripletexCountryDto Country { get; set; } = null!;
}

public class TripletexCustomerCreateDto
{
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? OrganizationNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsCustomer { get; set; } = true;
    public TripletexAddressDto PostalAddress { get; set; } = null!;
}
}
