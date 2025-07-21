namespace backend.Dtos
{
    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public int TripletexId { get; set; }
        public string? Status { get; set; }
        public decimal? Total { get; set; }
        public DateOnly InvoiceCreated { get; set; }
        public DateOnly InvoiceDueDate { get; set; }
        public int CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    public class InvoiceListResponse
    {
        public List<InvoiceDto> Values { get; set; } = new();
    }
}
