namespace backend.Dtos
{
    public class SaleOrderDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public string OrderNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public double TotalAmount { get; set; }
        public string OrderDate { get; set; } = "";
        public CustomerRefDto? Customer { get; set; }

        // Additional fields that Tripletex might return
        public string? DeliveryDate { get; set; }
        public string? InvoicesDueDate { get; set; }
        public string? InvoiceReference { get; set; }
        public string? OurReference { get; set; }
        public string? YourReference { get; set; }
        public string? Comment { get; set; }
    }

    public class CustomerRefDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class SaleOrderListResponse
    {
        public List<SaleOrderDto> Value { get; set; } = new();
        public int? Count { get; set; }
        public int? FullResultSize { get; set; }
        public int? From { get; set; }
    }
}