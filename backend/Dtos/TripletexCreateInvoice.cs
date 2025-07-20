
namespace backend.Dtos
{
    public class TripletexInvoiceCreateDto
{
    public TripletexCustomerRefDto? Customer { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public List<TripletexInvoiceLineDto>? InvoiceLines { get; set; }
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
