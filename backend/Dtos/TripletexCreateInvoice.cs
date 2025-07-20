
namespace backend.Dtos
{
    public class TripletexInvoiceCreateDto
    {
        public TripletexCustomerRefDto? Customer { get; set; }
        public string? InvoiceDate { get; set; }
        public CurrencyDto Currency { get; set; } = new CurrencyDto();
        public string? Status { get; set; }
        public List<TripletexInvoiceLineDto>? InvoiceLines { get; set; }
    }

public class CurrencyDto
{
    public string Code { get; set; } = "NOK";
}

    public class TripletexCustomerRefDto
    {
        public int Id { get; set; }
    }

public class TripletexInvoiceLineDto
{
    public TripletexProductRefDto? Product { get; set; }
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public string? Description { get; set; }
}

public class TripletexProductRefDto
{
    public int Id { get; set; }
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
