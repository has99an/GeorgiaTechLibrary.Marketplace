using WarehouseService.Models;
using Microsoft.EntityFrameworkCore;

namespace WarehouseService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<WarehouseItem> WarehouseItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure WarehouseItem entity
        modelBuilder.Entity<WarehouseItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BookISBN).IsRequired().HasMaxLength(13);
            entity.Property(e => e.SellerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Condition).IsRequired().HasMaxLength(50);
        });
    }
}
