using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Database context for UserService
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            
            entity.HasKey(e => e.UserId);
            
            entity.Property(e => e.UserId)
                .IsRequired();

            // Email as value object - store as string
            entity.Property(e => e.Email)
                .HasConversion(
                    email => email.Value,
                    value => Email.Create(value))
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Role)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.UpdatedDate)
                .IsRequired(false);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Configure Address value object as owned entity
            entity.OwnsOne(e => e.DeliveryAddress, address =>
            {
                address.Property(a => a.Street)
                    .HasColumnName("DeliveryStreet")
                    .HasMaxLength(200);

                address.Property(a => a.City)
                    .HasColumnName("DeliveryCity")
                    .HasMaxLength(100);

                address.Property(a => a.PostalCode)
                    .HasColumnName("DeliveryPostalCode")
                    .HasMaxLength(10);

                address.Property(a => a.State)
                    .HasColumnName("DeliveryState")
                    .HasMaxLength(100);

                address.Property(a => a.Country)
                    .HasColumnName("DeliveryCountry")
                    .HasMaxLength(100);
            });

            // Ensure email uniqueness
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            // Index on Role for faster queries
            entity.HasIndex(e => e.Role)
                .HasDatabaseName("IX_Users_Role");

            // Filter out deleted users by default
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}

