using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class User
    {
        [Key]
        public int Customerid { get; set; }

        public int TripletexId { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public required string PostalAddress { get; set; }

        [MaxLength(100)]
        public required string OrganizationNumber { get; set; }

        [MaxLength(100)]
        public required string PhoneNumber { get; set; }

        [MaxLength(20)]
        public required string PostalCode { get; set; }

        [MaxLength(100)]
        public required string AddressLine1 { get; set; }

        [MaxLength(100)]
        public required string City { get; set; }

        [MaxLength(100)]
        public required string Country { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public required string Email { get; set; } = string.Empty;
        
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}