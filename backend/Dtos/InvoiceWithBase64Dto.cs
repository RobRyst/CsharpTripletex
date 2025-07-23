
namespace backend.Dtos
{
    public class InvoiceWithBase64Dto
{
    public TripletexInvoiceCreateDto Invoice { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string FileBase64 { get; set; } = default!;
}

}
