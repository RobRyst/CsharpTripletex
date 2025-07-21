namespace backend.Dtos
{
    public class SaleOrderDto
    {
        public int Id { get; set; }
        public string? Number { get; set; }
        public string? Status { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Amount { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public CustomerRefDto? Customer { get; set; }
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