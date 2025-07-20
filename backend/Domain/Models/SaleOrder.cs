namespace backend.Domain.Models
{
    public class SaleOrderModel
    {
        public int Id { get; set; }
        public int TripletexId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double TotalAmount { get; set; }
        public DateOnly OrderDate { get; set; }

        public CustomerModel? Customer { get; set; }
    }

    public class CustomerModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
