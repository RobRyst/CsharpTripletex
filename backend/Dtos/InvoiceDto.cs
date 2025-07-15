namespace backend.Dtos
{
    public class InvoiceDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public double Total { get; set; }
        public DateOnly InvoiceCreated { get; set; }
        public DateOnly InvoiceDueDate { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
    }
}