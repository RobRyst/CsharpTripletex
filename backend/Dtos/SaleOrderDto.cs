namespace backend.Dtos
{
    public class SalesOrderDto
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

    public class SalesOrderListResponse
    {
        public List<SalesOrderDto> Value { get; set; } = new();
    }
}
