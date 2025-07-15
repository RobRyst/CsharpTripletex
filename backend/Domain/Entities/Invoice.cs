using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.Domain.Models;

namespace backend.Domain.Entities
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }
        public int TripletexId { get; set; }
        public required string Status { get; set; }
        public required double Total { get; set; }
        public required DateOnly InvoiceCreated { get; set; }
        public required DateOnly InvoiceDueDate { get; set; }

        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        
        public Customer? Customer { get; set; }
    }
}