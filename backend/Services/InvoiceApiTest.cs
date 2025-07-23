using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InvoiceApiTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
        var filePath = "invoice.pdf";
        var fileBytes = File.ReadAllBytes(filePath);
        var base64String = Convert.ToBase64String(fileBytes);
        File.WriteAllText("payload.txt", base64String);
        Console.WriteLine("✅ Base64 string written to payload.txt");

            var invoiceDto = new
            {
                invoice = new
                {
                    customer = new { id = 80389576 },
                    invoiceDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    invoiceDueDate = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"),
                    total = 200.00m
                },
                fileName = "invoice.pdf",
                fileBase64 = base64String,
                userId = "test-user"
            };

            var json = JsonSerializer.Serialize(invoiceDto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });



            using var client = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var base64 = Convert.ToBase64String(File.ReadAllBytes("invoice.pdf"));

            var response = await client.PostAsync("http://localhost:5045/invoice/with-attachment-json", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Response Status: " + response.StatusCode);
            Console.WriteLine("Response Body: " + result);
        }
    }
}
