namespace backend.Dtos
{
    public class SaleOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public double TotalAmount { get; set; }
    public string OrderDate { get; set; } = "";
    public CustomerRefDto Customer { get; set; } = new();
}

    public class CustomerRefDto
    {
        public int Id { get; set; }
    }

    public class SaleOrderListResponse
    {
        public List<SaleOrderDto> Value { get; set; } = new();
    }
}
