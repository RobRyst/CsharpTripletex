namespace backend.Dtos
{
using System.Text.Json.Serialization;

public class TripletexInvoiceCreateDto
{
    public TripletexCustomerRefDto Customer { get; set; } = default!;
    public string InvoiceDate { get; set; } = default!;
    public string InvoiceDueDate { get; set; } = default!;
    public TripletexCurrencyDto Currency { get; set; } = new TripletexCurrencyDto { Code = "NOK" };

    [JsonPropertyName("invoiceLine")]
    public List<TripletexInvoiceLineDto> InvoiceLines { get; set; } = new();
}

}


public class TripletexCurrencyDto
{
    public string Code { get; set; } = "NOK";
}

    public class TripletexCustomerRefDto
    {
        public int Id { get; set; }
    }

    public class TripletexInvoiceLineDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class TripletexResponseDto
    {
        public TripletexInvoiceDto? Value { get; set; }
    }

    public class TripletexInvoiceDto
    {
        public int Id { get; set; }
    }


