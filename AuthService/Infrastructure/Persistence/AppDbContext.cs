using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// Database context for AuthService
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AuthUser> AuthUsers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure AuthUser entity
        modelBuilder.Entity<AuthUser>(entity =>
        {
            entity.ToTable("AuthUsers");
            
            entity.HasKey(e => e.UserId);
            
            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.LastLoginDate)
                .IsRequired(false);

            entity.Property(e => e.FailedLoginAttempts)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.LockoutEndDate)
                .IsRequired(false);

            // Email as value object - store as string using HasConversion
            entity.Property(e => e.Email)
                .HasConversion(
                    email => email.Value,
                    value => Email.Create(value))
                .IsRequired()
                .HasMaxLength(255);

            // Ensure email uniqueness
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_AuthUsers_Email");
        });
    }
}

