namespace backend.Dtos
{
    public class CustomerDto
    {
        public int Customerid { get; set; }
        public int TripletexId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    public class CustomerListResponse
    {
        public List<CustomerDto>? Values { get; set; }
    }
}