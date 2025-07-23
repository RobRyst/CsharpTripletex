namespace backend.Domain.Entities
{
        public class LogConnection
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public string FromEndpoint { get; set; }
        public string ToEndpoint { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}