using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Seeds user data from CSV file
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting user data seeding from CSV...");

            // Check if data already exists
            var existingCount = await context.Users.CountAsync();
            if (existingCount > 0)
            {
                logger.LogInformation("Database already contains {Count} users. Skipping seed.", existingCount);
                return;
            }

            var users = LoadUsersFromCsv(logger);

            if (!users.Any())
            {
                logger.LogWarning("No users loaded from CSV file.");
                return;
            }

            // Validate role distribution
            var roleStats = users.GroupBy(u => u.Role)
                .ToDictionary(g => g.Key, g => g.Count());

            logger.LogInformation("Role distribution: Student={Student}, Seller={Seller}, Admin={Admin}",
                roleStats.GetValueOrDefault(UserRole.Student, 0),
                roleStats.GetValueOrDefault(UserRole.Seller, 0),
                roleStats.GetValueOrDefault(UserRole.Admin, 0));

            // Use execution strategy to support retries with transactions
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    int batchSize = 100;
                    int totalInserted = 0;

                    for (int i = 0; i < users.Count; i += batchSize)
                    {
                        var batch = users.Skip(i).Take(batchSize).ToList();
                        
                        // Clear change tracker before adding new batch to avoid duplicate tracking
                        context.ChangeTracker.Clear();
                        
                        await context.Users.AddRangeAsync(batch);
                        await context.SaveChangesAsync();
                        
                        totalInserted += batch.Count;
                        logger.LogInformation("Inserted batch {BatchNumber}: {Count} users (Total: {Total})",
                            i / batchSize + 1, batch.Count, totalInserted);
                    }

                    await transaction.CommitAsync();
                    logger.LogInformation("Successfully seeded {Count} users from CSV.", totalInserted);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    logger.LogError(ex, "Transaction failed during seeding. Rolling back.");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding users from CSV.");
            throw;
        }
    }

    private static List<User> LoadUsersFromCsv(ILogger logger)
    {
        var users = new List<User>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Users_Small.csv");

        if (!File.Exists(csvPath))
        {
            logger.LogError("CSV file not found: {Path}", csvPath);
            return users;
        }

        try
        {
            using var reader = new StreamReader(csvPath);
            
            // Skip header row
            var header = reader.ReadLine();
            logger.LogInformation("CSV Header: {Header}", header);

            int lineNumber = 1;
            int successCount = 0;
            int errorCount = 0;

            while (!reader.EndOfStream)
            {
                lineNumber++;
                var line = reader.ReadLine();
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var values = line.Split(',');

                    if (values.Length < 5)
                    {
                        logger.LogWarning("Line {LineNumber}: Insufficient columns ({Count})", lineNumber, values.Length);
                        errorCount++;
                        continue;
                    }

                    // Parse CSV fields
                    var userId = Guid.Parse(values[0].Trim());
                    var email = values[1].Trim();
                    var name = values[2].Trim();
                    var roleString = values[3].Trim();
                    var createdDate = DateTime.Parse(values[4].Trim());

                    // Validate and parse role
                    if (!Enum.TryParse<UserRole>(roleString, true, out var role))
                    {
                        logger.LogWarning("Line {LineNumber}: Invalid role '{Role}', defaulting to Student", 
                            lineNumber, roleString);
                        role = UserRole.Student;
                    }

                    // Create user entity with validation
                    var user = User.CreateWithId(userId, email, name, role, createdDate);
                    users.Add(user);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Line {LineNumber}: Failed to parse user data: {Line}", 
                        lineNumber, line);
                    errorCount++;
                }
            }

            logger.LogInformation("CSV parsing complete. Success: {Success}, Errors: {Errors}", 
                successCount, errorCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading CSV file: {Path}", csvPath);
        }

        return users;
    }
}

