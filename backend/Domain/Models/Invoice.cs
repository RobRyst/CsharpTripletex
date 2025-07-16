using System.ComponentModel.DataAnnotations.Schema;
using backend.Domain.Models;

public class Invoice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    [ForeignKey("CustomerId")]
    public Customer? Customer { get; set; }

    public DateOnly InvoiceCreated { get; set; }
    public DateOnly InvoiceDueDate { get; set; }
    public string? Status { get; set; }
    public double Total { get; set; }
    public int TripletexId { get; set; }
}
