using Microsoft.EntityFrameworkCore;
using backend.Domain.Entities;

namespace backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<SaleOrder> Saleorders { get; set; }
        public DbSet<LogConnection> LogConnections { get; set; } 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedOnAdd();
    entity.Property(e => e.TripletexId).IsRequired();
    entity.Property(e => e.Name).HasMaxLength(255);
    entity.Property(e => e.Email).HasMaxLength(255);
    entity.HasIndex(e => e.TripletexId).IsUnique();

    entity.HasMany(c => c.Invoices)
          .WithOne(i => i.Customer)
          .HasForeignKey(i => i.CustomerId)
          .OnDelete(DeleteBehavior.Cascade);

    entity.HasMany(c => c.SaleOrders)
          .WithOne(so => so.Customer)
          .HasForeignKey(so => so.CustomerId)
          .OnDelete(DeleteBehavior.Cascade);
});

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.TripletexId);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.InvoiceCreated).IsRequired();
                entity.Property(e => e.InvoiceDueDate).IsRequired();
                entity.Property(e => e.InvoiceDate).IsRequired();
                entity.Property(e => e.DueDate).IsRequired();
                entity.Property(e => e.Currency).HasMaxLength(10);
                entity.Property(e => e.CustomerTripletexId).IsRequired();

                entity.HasIndex(e => e.TripletexId).IsUnique();

                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Invoices)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SaleOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.TripletexId);
                entity.Property(e => e.Number).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.OrderDate).IsRequired();

                entity.HasIndex(e => e.TripletexId).IsUnique();

                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.SaleOrders)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
