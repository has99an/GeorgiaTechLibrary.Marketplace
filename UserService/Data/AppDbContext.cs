using UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace UserService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();

            // Ensure email uniqueness
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
