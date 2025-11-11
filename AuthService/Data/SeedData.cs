using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        context.ChangeTracker.Clear();

        // Check if any auth users exist
        if (await context.AuthUsers.AnyAsync())
        {
            Console.WriteLine("AuthUsers already exist in database. Skipping seed data.");
            return;
        }

        // Seed some default auth users (with dummy UserIds for testing)
        var authUsers = new List<AuthUser>
        {
            new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = "admin@georgiatech.edu",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                CreatedDate = DateTime.UtcNow
            },
            new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = "student@georgiatech.edu",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("student123"),
                CreatedDate = DateTime.UtcNow
            },
            new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = "seller@georgiatech.edu",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("seller123"),
                CreatedDate = DateTime.UtcNow
            }
        };

        try
        {
            await context.AuthUsers.AddRangeAsync(authUsers);
            await context.SaveChangesAsync();
            Console.WriteLine($"Successfully seeded {authUsers.Count} default auth users to database.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding auth users to database: {ex.Message}");
        }
    }
}
