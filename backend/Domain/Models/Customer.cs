namespace backend.Domain.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public int TripletexId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}