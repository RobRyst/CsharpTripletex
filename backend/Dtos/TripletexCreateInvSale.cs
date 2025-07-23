namespace backend.Dtos
{
    public class TripletexOrderGetResponse
    {
        public TripletexOrderDto? Value { get; set; }
    }

    public class TripletexOrderDto
    {
        public string OrderDate { get; set; } = default!;
        public string DeliveryDate { get; set; } = default!;
        public TripletexCustomerRefDto Customer { get; set; } = default!;
        public List<TripletexOrderLineDto> OrderLines { get; set; } = new();
        public PreliminaryInvoiceInfo? PreliminaryInvoice { get; set; }
    }

    public class PreliminaryInvoiceInfo
    {
        public long Id { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class TripletexOrderLineDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Count { get; set; }
        public decimal UnitPriceExcludingVatCurrency { get; set; }
    }

    public class TripletexCustomerRefDto
    {
        public int Id { get; set; }
    }

    public class TripletexCreateSaleOrder
    {
        public long? TripletexId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string OrderDate { get; set; } = string.Empty;
        public int CustomerId { get; set; }
    }

    public class TripletexInvoiceResponse
    {
        public TripletexInvoiceValue? Value { get; set; }
    }

    public class TripletexInvoiceValue
    {
        public long Id { get; set; }
        public string? InvoiceDate { get; set; }
        public string? InvoiceDueDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "Consulting services";
        public CustomerRef? Customer { get; set; }
        public List<OrderRef>? Orders { get; set; }
    }

    public class CustomerRef
    {
        public long Id { get; set; }
        public string? Url { get; set; }
    }

    public class OrderRef
    {
        public long Id { get; set; }
        public string? Url { get; set; }
    }

    public class TripletexOrderResponse
    {
        public TripletexOrderValue? Value { get; set; }
    }

    public class TripletexOrderValue
    {
        public long Id { get; set; }
    }

    public class TripletexInvoiceCreateDto
    {
        public TripletexCustomerRefDto Customer { get; set; } = default!;
        public string InvoiceDate { get; set; } = default!;
        public string InvoiceDueDate { get; set; } = default!;
        public TripletexCurrencyRefDto Currency { get; set; } = new TripletexCurrencyRefDto { Id = 1 };
        public List<TripletexOrderDto> Orders { get; set; } = new();
    }

    public class TripletexCurrencyRefDto
    {
        public int Id { get; set; }
    }

    public class TripletexResponseDto
    {
        public TripletexInvoiceValue? Value { get; set; }
    }

    public class InvoiceValueDto
    {
        public int Id { get; set; }
        public List<OrderDto> Orders { get; set; } = new();
    }

    public class OrderDto
    {
        public int Id { get; set; }
    }

    public class TripletexInvoiceDto
    {
        public int Id { get; set; }
    }
}
