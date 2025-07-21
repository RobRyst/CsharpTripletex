using System.Text.Json.Serialization;

namespace backend.Dtos
{
    public class TripletexInvoiceCreateDto
    {
        public TripletexCustomerRefDto Customer { get; set; } = default!;
        public string InvoiceDate { get; set; } = default!;
        public string InvoiceDueDate { get; set; } = default!;
        public TripletexCurrencyRefDto Currency { get; set; } = new TripletexCurrencyRefDto { Id = 1 };

        // Orders now contain full order structure
        public List<TripletexOrderDto> Orders { get; set; } = new();
    }

    public class TripletexCustomerRefDto
    {
        public int Id { get; set; }
    }

    public class TripletexCurrencyRefDto
    {
        public int Id { get; set; }
    }

    public class TripletexOrderDto
    {
        public string OrderDate { get; set; } = default!;
        public string DeliveryDate { get; set; } = default!;
        public TripletexCustomerRefDto Customer { get; set; } = default!;

        [JsonPropertyName("orderLines")]
        public List<TripletexOrderLineDto> OrderLines { get; set; } = new();
    }

    public class TripletexOrderLineDto
    {
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public decimal Count { get; set; }

        [JsonPropertyName("unitPriceExcludingVatCurrency")]
        public decimal UnitPriceExcludingVatCurrency { get; set; }
    }

    public class TripletexResponseDto
    {
        public TripletexInvoiceDto? Value { get; set; }
    }

    public class TripletexInvoiceDto
    {
        public int Id { get; set; }
    }
}
