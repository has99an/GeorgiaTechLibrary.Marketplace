using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// Seeds authentication data from CSV file
/// </summary>
public static class SeedData
{
    // Default password for all seeded users (users must reset on first login)
    private const string DefaultPassword = "Password123!";

    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting authentication data seeding from CSV...");

            // Check if data already exists
            var existingCount = await context.AuthUsers.CountAsync();
            if (existingCount > 0)
            {
                logger.LogInformation("Database already contains {Count} auth users. Skipping seed.", existingCount);
                return;
            }

            var authUsers = LoadAuthUsersFromCsv(logger);

            if (!authUsers.Any())
            {
                logger.LogWarning("No auth users loaded from CSV file.");
                return;
            }

            logger.LogInformation("Loaded {Count} auth users from CSV. Starting batch insert...", authUsers.Count);

            // Insert in batches with transaction
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                int batchSize = 100;
                int totalInserted = 0;

                for (int i = 0; i < authUsers.Count; i += batchSize)
                {
                    var batch = authUsers.Skip(i).Take(batchSize).ToList();
                    await context.AuthUsers.AddRangeAsync(batch);
                    await context.SaveChangesAsync();
                    
                    totalInserted += batch.Count;
                    logger.LogInformation("Inserted batch {BatchNumber}: {Count} auth users (Total: {Total})",
                        i / batchSize + 1, batch.Count, totalInserted);
                }

                await transaction.CommitAsync();
                logger.LogInformation("Successfully seeded {Count} auth users from CSV.", totalInserted);
                logger.LogWarning("All seeded users have default password: {Password}. Users must reset on first login.", DefaultPassword);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Transaction failed during seeding. Rolling back.");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding auth users from CSV.");
            throw;
        }
    }

    private static List<AuthUser> LoadAuthUsersFromCsv(ILogger logger)
    {
        var authUsers = new List<AuthUser>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "AuthUsers.csv");

        if (!File.Exists(csvPath))
        {
            logger.LogError("CSV file not found: {Path}", csvPath);
            return authUsers;
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

            // Hash the default password once
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);

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

                    if (values.Length < 4)
                    {
                        logger.LogWarning("Line {LineNumber}: Insufficient columns ({Count})", lineNumber, values.Length);
                        errorCount++;
                        continue;
                    }

                    // Parse CSV fields
                    var userId = Guid.Parse(values[0].Trim());
                    var email = values[1].Trim();
                    // values[2] is the simulated hash - we ignore it and use real bcrypt hash
                    var createdDate = DateTime.Parse(values[3].Trim());

                    // Create auth user entity with validation
                    // Use default password hash for all users (they need to reset)
                    var authUser = AuthUser.CreateWithId(userId, email, defaultPasswordHash, createdDate);
                    authUsers.Add(authUser);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Line {LineNumber}: Failed to parse auth user data: {Line}", 
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

        return authUsers;
    }
}

