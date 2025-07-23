using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Domain.Entities
{
    public class Invoice
{
    [Key]
    public int Id { get; set; }

    public int? TripletexId { get; set; }
    public string Status { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public DateOnly InvoiceCreated { get; set; }

    public DateOnly InvoiceDueDate { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public DateOnly DueDate { get; set; }

    public string Currency { get; set; } = "NOK";

    public int? VoucherId { get; set; }

    public int CustomerTripletexId { get; set; }

    [ForeignKey("Customer")]
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }
    }

}