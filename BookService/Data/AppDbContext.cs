using BookService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.ISBN);
            entity.Property(e => e.ISBN).HasMaxLength(13);
            entity.Property(e => e.BookTitle).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BookAuthor).IsRequired().HasMaxLength(200);
            entity.Property(e => e.YearOfPublication).IsRequired();
            entity.Property(e => e.Publisher).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ImageUrlS).HasMaxLength(500);
            entity.Property(e => e.ImageUrlM).HasMaxLength(500);
            entity.Property(e => e.ImageUrlL).HasMaxLength(500);
            
            // 👇 DE 8 NYE FELTER
            entity.Property(e => e.Genre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PageCount).IsRequired();
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.AvailabilityStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Edition).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(50);
        });
    }
}