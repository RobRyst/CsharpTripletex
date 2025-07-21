namespace backend.Dtos
{
    public class TripletexCreateSaleOrder
    {
        public int TripletexId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string OrderDate { get; set; } = string.Empty;
        public int CustomerId { get; set; }
    }
}