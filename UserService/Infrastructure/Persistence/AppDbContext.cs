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
    public DbSet<SellerProfile> SellerProfiles { get; set; } = null!;
    public DbSet<SellerBookListing> SellerBookListings { get; set; } = null!;
    public DbSet<SellerReview> SellerReviews { get; set; } = null!;

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

        // Configure SellerProfile entity
        modelBuilder.Entity<SellerProfile>(entity =>
        {
            entity.ToTable("SellerProfiles");
            
            entity.HasKey(e => e.SellerId);
            
            entity.Property(e => e.SellerId)
                .IsRequired();

            entity.Property(e => e.Rating)
                .IsRequired()
                .HasColumnType("decimal(3,2)")
                .HasDefaultValue(0.0m);

            entity.Property(e => e.TotalSales)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.TotalBooksSold)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.Location)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.UpdatedDate)
                .IsRequired(false);

            // Foreign key relationship to User (optional to avoid query filter issues)
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<SellerProfile>(e => e.SellerId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Indexes
            entity.HasIndex(e => e.Rating)
                .HasDatabaseName("IX_SellerProfiles_Rating");

            entity.HasIndex(e => e.Location)
                .HasDatabaseName("IX_SellerProfiles_Location");
        });

        // Configure SellerBookListing entity
        modelBuilder.Entity<SellerBookListing>(entity =>
        {
            entity.ToTable("SellerBookListings");
            
            entity.HasKey(e => e.ListingId);
            
            entity.Property(e => e.ListingId)
                .IsRequired();

            entity.Property(e => e.SellerId)
                .IsRequired();

            entity.Property(e => e.BookISBN)
                .IsRequired()
                .HasMaxLength(13);

            entity.Property(e => e.Price)
                .IsRequired()
                .HasColumnType("decimal(10,2)");

            entity.Property(e => e.Quantity)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.Condition)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.UpdatedDate)
                .IsRequired(false);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Foreign key relationship to SellerProfile
            entity.HasOne(e => e.SellerProfile)
                .WithMany()
                .HasForeignKey(e => e.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: One listing per seller per ISBN per condition
            entity.HasIndex(e => new { e.SellerId, e.BookISBN, e.Condition })
                .IsUnique()
                .HasDatabaseName("IX_SellerBookListings_SellerId_BookISBN_Condition");

            // Indexes
            entity.HasIndex(e => e.SellerId)
                .HasDatabaseName("IX_SellerBookListings_SellerId");

            entity.HasIndex(e => e.BookISBN)
                .HasDatabaseName("IX_SellerBookListings_BookISBN");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_SellerBookListings_IsActive");
        });

        // Configure SellerReview entity
        modelBuilder.Entity<SellerReview>(entity =>
        {
            entity.ToTable("SellerReviews");
            
            entity.HasKey(e => e.ReviewId);
            
            entity.Property(e => e.ReviewId)
                .IsRequired();

            entity.Property(e => e.SellerId)
                .IsRequired();

            entity.Property(e => e.OrderId)
                .IsRequired();

            entity.Property(e => e.CustomerId)
                .IsRequired();

            entity.Property(e => e.Rating)
                .IsRequired()
                .HasColumnType("decimal(3,2)");

            entity.Property(e => e.Comment)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.UpdatedDate)
                .IsRequired(false);

            // Foreign key relationship to SellerProfile
            entity.HasOne(e => e.SellerProfile)
                .WithMany()
                .HasForeignKey(e => e.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: One review per customer per order per seller
            entity.HasIndex(e => new { e.OrderId, e.SellerId, e.CustomerId })
                .IsUnique()
                .HasDatabaseName("IX_SellerReviews_OrderId_SellerId_CustomerId");

            // Indexes
            entity.HasIndex(e => e.SellerId)
                .HasDatabaseName("IX_SellerReviews_SellerId");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("IX_SellerReviews_OrderId");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("IX_SellerReviews_CustomerId");

            entity.HasIndex(e => e.Rating)
                .HasDatabaseName("IX_SellerReviews_Rating");
        });
    }
}

