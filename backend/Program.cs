using backend.Domain.interfaces;
using Microsoft.EntityFrameworkCore;
using backend.Services;
using backend.Infrastructure.Data;
using backend.Domain.Interfaces;
using backend.Repository;
using QuestPDF.Infrastructure;
using backend.Domain.Entities;


var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<ITokenService, TokenService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<SaleOrderService>();
builder.Services.AddScoped<ISaleOrderService, SaleOrderService>();
builder.Services.AddScoped<ImportSaleOrderService>();


builder.Services.AddLogging();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Customers.Any(c => c.TripletexId == 3))
    {
        var customer = new Customer
        {
            Name = "Testkunde",
            Email = "test@example.com",
            TripletexId = 3,
            PhoneNumber = "12345678",
            AddressLine1 = "Testveien 1",
            City = "Oslo",
            PostalCode = "0123",
            Country = "Norway"
        };

        db.Customers.Add(customer);
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

