using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public static class SeedData
{
public static async Task Initialize(AppDbContext context)
{
    const int maxRetries = 10;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
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

            await context.AuthUsers.AddRangeAsync(authUsers);
            await context.SaveChangesAsync();
            Console.WriteLine($"Successfully seeded {authUsers.Count} default auth users to database.");
            return;
        }
        catch (Exception ex)
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                Console.WriteLine($"Error seeding auth users to database after {maxRetries} retries: {ex.Message}");
                return;
            }

            Console.WriteLine($"Seeding failed (attempt {retryCount}/{maxRetries}): {ex.Message}. Retrying in 2 seconds...");
            await Task.Delay(2000);
        }
    }
}
}
