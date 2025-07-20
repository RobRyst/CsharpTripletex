using backend.Domain.Models;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using backend.Mappers;

namespace backend.Services
{
    public class CustomerImportService
    {
        private readonly TripleTexService _client;
        private readonly AppDbContext _db;

        public CustomerImportService(TripleTexService client, AppDbContext db)
        {
            _client = client;
            _db = db;
        }

        public async Task ImportCustomersAsync()
        {
            var customerDtos = await _client.GetCustomersAsync();

            foreach (var dto in customerDtos)
            {
                var model = CustomerDtoMapper.ToModel(dto);
                var entity = CustomerMapper.ToEntity(model);
                _db.Customers.Add(entity);
            }

            await _db.SaveChangesAsync();
        }
    }
}

