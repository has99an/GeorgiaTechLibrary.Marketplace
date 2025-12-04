using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.ValueObjects;

namespace OrderService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<ShoppingCart> ShoppingCarts { get; set; } = null!;
    public DbSet<CartItem> CartItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.OrderId);

            entity.Property(o => o.CustomerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(o => o.OrderDate)
                .IsRequired();

            entity.Property(o => o.Status)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<OrderStatus>(v));

            // Configure Money value object
            entity.OwnsOne(o => o.TotalAmount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("TotalAmount")
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            // Configure Address value object as owned entity
            entity.OwnsOne(o => o.DeliveryAddress, address =>
            {
                address.Property(a => a.Street)
                    .HasColumnName("DeliveryStreet")
                    .HasMaxLength(200)
                    .IsRequired();

                address.Property(a => a.City)
                    .HasColumnName("DeliveryCity")
                    .HasMaxLength(100)
                    .IsRequired();

                address.Property(a => a.PostalCode)
                    .HasColumnName("DeliveryPostalCode")
                    .HasMaxLength(10)
                    .IsRequired();

                address.Property(a => a.State)
                    .HasColumnName("DeliveryState")
                    .HasMaxLength(100);

                address.Property(a => a.Country)
                    .HasColumnName("DeliveryCountry")
                    .HasMaxLength(100);
            });

            entity.Property(o => o.CancellationReason)
                .HasMaxLength(500);

            entity.Property(o => o.RefundReason)
                .HasMaxLength(500);

            entity.HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.OrderDate);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(oi => oi.OrderItemId);

            entity.Property(oi => oi.BookISBN)
                .IsRequired()
                .HasMaxLength(13);

            entity.Property(oi => oi.SellerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(oi => oi.Quantity)
                .IsRequired();

            entity.Property(oi => oi.Status)
                .IsRequired()
                .HasMaxLength(20);

            // Configure Money value object
            entity.OwnsOne(oi => oi.UnitPrice, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            entity.HasIndex(oi => oi.BookISBN);
            entity.HasIndex(oi => oi.SellerId);
        });

        // Configure ShoppingCart entity
        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.ToTable("ShoppingCarts");
            entity.HasKey(sc => sc.ShoppingCartId);

            entity.Property(sc => sc.CustomerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(sc => sc.CreatedDate)
                .IsRequired();

            entity.HasMany(sc => sc.Items)
                .WithOne(ci => ci.ShoppingCart)
                .HasForeignKey("ShoppingCartId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(sc => sc.CustomerId)
                .IsUnique();
        });

        // Configure CartItem entity
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems");
            entity.HasKey(ci => ci.CartItemId);

            entity.Property(ci => ci.BookISBN)
                .IsRequired()
                .HasMaxLength(13);

            entity.Property(ci => ci.SellerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(ci => ci.Quantity)
                .IsRequired();

            entity.Property(ci => ci.AddedDate)
                .IsRequired();

            // Configure Money value object
            entity.OwnsOne(ci => ci.UnitPrice, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            entity.HasIndex(ci => new { ci.ShoppingCartId, ci.BookISBN, ci.SellerId });
        });
    }
}

