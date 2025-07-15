using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class User
    {
        [Key]
        public int id { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public required string Address { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public required string Email { get; set; } = string.Empty;
        
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}