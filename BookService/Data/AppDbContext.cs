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

        // Configure Book entity
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
        });
    }
}
