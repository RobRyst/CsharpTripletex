using System.ComponentModel.DataAnnotations.Schema;
using backend.Domain.Entities;
using backend.Domain.Models;

namespace backend.Domain.Models
{
    public class InvoiceModel
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Currency { get; set; }

    public int CustomerTripletexId => Customer?.TripletexId ?? 0;

    public DateOnly InvoiceDate => InvoiceCreated;

    public DateOnly DueDate => InvoiceDueDate;
    public CustomerModel? Customer { get; set; }

    public DateOnly InvoiceCreated { get; set; }
    public DateOnly InvoiceDueDate { get; set; }
    public string? Status { get; set; }
    public double Total { get; set; }
    public int TripletexId { get; set; }
}   
}
