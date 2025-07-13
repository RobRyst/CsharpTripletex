// Create this file at: backend/Infrastructure/Data/AppDbContext.cs

using Microsoft.EntityFrameworkCore;
using backend.Domain.Models;

namespace backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.TripletexId).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Email).HasMaxLength(255);
                
                // Create unique index on TripletexId to prevent duplicates
                entity.HasIndex(e => e.TripletexId).IsUnique();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}