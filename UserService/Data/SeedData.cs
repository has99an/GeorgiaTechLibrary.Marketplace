using UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace UserService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        try
        {
            Console.WriteLine("Seeding user data from CSV...");
            
            // FJERN ALLE EKSISTERENDE BRUGERE FØRST
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
            
            var users = LoadUsersFromCsv();

            if (users.Any())
            {
                // USE TRANSACTION WITH IDENTITY_INSERT
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // INSERT IN BATCHES
                    int batchSize = 100;
                    for (int i = 0; i < users.Count; i += batchSize)
                    {
                        var batch = users.Skip(i).Take(batchSize).ToList();
                        await context.Users.AddRangeAsync(batch);
                        await context.SaveChangesAsync();
                        Console.WriteLine($"Inserted batch {i/batchSize + 1}: {batch.Count} users");
                    }
                    await transaction.CommitAsync();
                    Console.WriteLine($"Successfully seeded {users.Count} users from CSV.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Transaction failed: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine("No users found to seed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding users: {ex.Message}");
        }
    }

    private static List<User> LoadUsersFromCsv()
    {
        var users = new List<User>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Users_Clean.csv");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"CSV file not found: {csvPath}");
            return users;
        }

        try
        {
            using var reader = new StreamReader(csvPath);
            // Skip header row
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line?.Split(',') ?? Array.Empty<string>();

                if (values.Length >= 5)
                {
                    var user = new User
                    {
                        UserId = Guid.Parse(values[0]),
                        Email = values[1],
                        Name = values[2],
                        Role = Enum.Parse<UserRole>(values[3]),
                        CreatedDate = DateTime.Parse(values[4])
                    };
                    users.Add(user);
                }
            }

            Console.WriteLine($"Loaded {users.Count} users from CSV.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading CSV data: {ex.Message}");
        }

        return users;
    }
}