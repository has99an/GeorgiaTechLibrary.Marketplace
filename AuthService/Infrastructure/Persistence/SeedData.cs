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

            // Remove duplicates by email before inserting
            var uniqueAuthUsers = authUsers
                .GroupBy(u => u.Email.Value)
                .Select(g => g.First())
                .ToList();

            if (uniqueAuthUsers.Count < authUsers.Count)
            {
                logger.LogWarning("Removed {DuplicateCount} duplicate emails from {TotalCount} users",
                    authUsers.Count - uniqueAuthUsers.Count, authUsers.Count);
            }

            logger.LogInformation("Loaded {Count} unique auth users from CSV. Starting batch insert...", uniqueAuthUsers.Count);

            // Use execution strategy to support retries with transactions
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    int batchSize = 100;
                    int totalInserted = 0;
                    int totalSkipped = 0;

                    for (int i = 0; i < uniqueAuthUsers.Count; i += batchSize)
                    {
                        var batch = uniqueAuthUsers.Skip(i).Take(batchSize).ToList();
                        
                        // Clear change tracker before adding new batch to avoid duplicate tracking
                        context.ChangeTracker.Clear();
                        
                        // Check which users already exist in database (by email)
                        // With HasConversion, we need to materialize Email objects first, then compare
                        var batchEmails = batch.Select(b => b.Email).ToList();
                        
                        // Load all existing users and materialize their emails
                        var existingUsers = await context.AuthUsers
                            .AsNoTracking()
                            .ToListAsync();
                        
                        var existingEmails = existingUsers
                            .Select(u => u.Email)
                            .Where(email => batchEmails.Any(be => be.Value == email.Value))
                            .ToList();
                        
                        var newUsers = batch
                            .Where(u => !existingEmails.Any(ee => ee.Value == u.Email.Value))
                            .ToList();
                        
                        int batchSkipped = batch.Count - newUsers.Count; // Already in DB
                        int batchInserted = 0;
                        
                        if (newUsers.Any())
                        {
                            try
                            {
                                await context.AuthUsers.AddRangeAsync(newUsers);
                                await context.SaveChangesAsync();
                                batchInserted = newUsers.Count;
                            }
                            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_AuthUsers_Email") == true || 
                                                                                                ex.InnerException?.Message.Contains("UNIQUE KEY") == true ||
                                                                                                ex.InnerException?.Message.Contains("duplicate key") == true)
                            {
                                // Handle unique constraint violation - try inserting one by one
                                logger.LogWarning("Batch insert failed due to duplicate email, inserting individually...");
                                foreach (var user in newUsers)
                                {
                                    try
                                    {
                                        context.ChangeTracker.Clear();
                                        await context.AuthUsers.AddAsync(user);
                                        await context.SaveChangesAsync();
                                        batchInserted++;
                                    }
                                    catch (Microsoft.EntityFrameworkCore.DbUpdateException)
                                    {
                                        // Skip duplicates silently
                                        batchSkipped++;
                                    }
                                }
                            }
                        }
                        
                        totalInserted += batchInserted;
                        totalSkipped += batchSkipped;
                        
                        logger.LogInformation("Processed batch {BatchNumber}: Inserted {Inserted}, Skipped {Skipped} (Total: {Total})",
                            i / batchSize + 1, batchInserted, batchSkipped, totalInserted);
                    }

                    await transaction.CommitAsync();
                    logger.LogInformation("Successfully seeded {Count} auth users from CSV. Skipped {Skipped} duplicates.", 
                        totalInserted, totalSkipped);
                    logger.LogWarning("All seeded users have default password: {Password}. Users must reset on first login.", DefaultPassword);
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
            logger.LogError(ex, "Error seeding auth users from CSV.");
            throw;
        }
    }

    private static List<AuthUser> LoadAuthUsersFromCsv(ILogger logger)
    {
        var authUsers = new List<AuthUser>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "AuthUsers_Small.csv");

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
                    // Default role is "Student" - will be synced from UserService if different
                    var authUser = AuthUser.CreateWithId(userId, email, defaultPasswordHash, "Student", createdDate);
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

