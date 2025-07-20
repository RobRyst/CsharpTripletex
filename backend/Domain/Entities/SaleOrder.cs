using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.Domain.Models;

namespace backend.Domain.Entities
{
    public class SaleOrder
    {
        [Key]
        public int Id { get; set; }

        public int TripletexId { get; set; }

        public required string OrderNumber { get; set; } = string.Empty;

        public required string Status { get; set; } = string.Empty;

        public double TotalAmount { get; set; }

        public DateOnly OrderDate { get; set; }

        [ForeignKey("Customer")]
        public int? CustomerId { get; set; }

        public CustomerModel? Customer { get; set; }
    }
}
