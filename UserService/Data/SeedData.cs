using UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace UserService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        context.ChangeTracker.Clear();

        // Check if any users exist
        if (await context.Users.AnyAsync())
        {
            Console.WriteLine("Users already exist in database. Skipping seed data.");
            return;
        }

        // Seed some default users
        var users = new List<User>
        {
            new User
            {
                UserId = Guid.NewGuid(),
                Email = "admin@georgiatech.edu",
                Name = "System Administrator",
                Role = UserRole.Admin,
                CreatedDate = DateTime.UtcNow
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Email = "student@georgiatech.edu",
                Name = "John Student",
                Role = UserRole.Student,
                CreatedDate = DateTime.UtcNow
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Email = "seller@georgiatech.edu",
                Name = "Jane Seller",
                Role = UserRole.Seller,
                CreatedDate = DateTime.UtcNow
            }
        };

        try
        {
            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
            Console.WriteLine($"Successfully seeded {users.Count} default users to database.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding users to database: {ex.Message}");
        }
    }
}
