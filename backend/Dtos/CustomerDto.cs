namespace backend.Dtos
{
    public class CustomerDto
    {
        public int Customerid { get; set; }
        public int TripletexId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? OrganizationNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PostalAddress { get; set; }
        public string? AddressLine1 { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
    
    public class CustomerListResponse
    {
        public List<CustomerDto>? Values { get; set; }
    }
}

}