using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Service that runs database migrations on application startup
/// </summary>
public class MigrationRunner : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IServiceProvider serviceProvider, ILogger<MigrationRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database migration runner...");

        const int maxRetries = 10;
        const int delaySeconds = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                _logger.LogInformation("Attempting database migration (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);
                
                // Ensure delivery address columns exist (manual migration if needed)
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(@"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'DeliveryStreet')
                        BEGIN
                            ALTER TABLE Users ADD DeliveryStreet NVARCHAR(200) NULL;
                            ALTER TABLE Users ADD DeliveryCity NVARCHAR(100) NULL;
                            ALTER TABLE Users ADD DeliveryPostalCode NVARCHAR(10) NULL;
                            ALTER TABLE Users ADD DeliveryState NVARCHAR(100) NULL;
                            ALTER TABLE Users ADD DeliveryCountry NVARCHAR(100) NULL;
                        END
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'DeliveryState')
                        BEGIN
                            ALTER TABLE Users ADD DeliveryState NVARCHAR(100) NULL;
                        END
                    ", cancellationToken);
                    _logger.LogInformation("Delivery address columns ensured");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not ensure delivery address columns (may already exist)");
                }
                
                // Apply all pending migrations
                await dbContext.Database.MigrateAsync(cancellationToken);
                
                _logger.LogInformation("Database migrations applied successfully. Starting seed data...");
                
                // Run seed data
                await SeedData.InitializeAsync(dbContext, _logger);
                
                _logger.LogInformation("Seed data completed successfully");
                return; // Success - exit retry loop
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database migration failed (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                
                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay} seconds...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to apply database migrations after {MaxRetries} attempts", maxRetries);
                    // Don't throw - allow app to start even if migrations fail (for debugging)
                    // In production, you might want to throw here
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Migration runner stopped");
        return Task.CompletedTask;
    }
}

