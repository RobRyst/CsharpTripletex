namespace backend.Domain.Models
{
    public class SaleOrderModel
    {
        public int Id { get; set; }
        public int TripletexId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateOnly OrderDate { get; set; }

        public int CustomerId { get; set; }
        public CustomerModel? Customer { get; set; }

    }
}
