using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        public int TripletexId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PostalAddress { get; set; } = string.Empty;
        public string OrganizationNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
