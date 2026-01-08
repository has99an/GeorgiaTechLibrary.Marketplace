using OrderService.Infrastructure.Caching;
using StackExchange.Redis;

namespace OrderService.Infrastructure.Jobs;

/// <summary>
/// Background job to clean up expired checkout sessions from Redis
/// Runs hourly to prevent memory buildup
/// </summary>
public class CleanupExpiredSessionsJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupExpiredSessionsJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public CleanupExpiredSessionsJob(
        IServiceProvider serviceProvider,
        ILogger<CleanupExpiredSessionsJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanupExpiredSessionsJob started");

        // Wait 5 minutes before first run to allow services to start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired sessions");
            }

            // Wait for next interval
            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("CleanupExpiredSessionsJob stopped");
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        _logger.LogInformation("Starting cleanup of expired checkout sessions");

        using var scope = _serviceProvider.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var database = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());

        try
        {
            // Scan for all checkout session keys
            var sessionKeys = server.Keys(pattern: "checkout:session:*", pageSize: 100).ToList();
            
            _logger.LogInformation("Found {Count} checkout session keys", sessionKeys.Count);

            var expiredCount = 0;
            var activeCount = 0;

            foreach (var key in sessionKeys)
            {
                var ttl = await database.KeyTimeToLiveAsync(key);
                
                // If key has no TTL or is already expired (shouldn't happen but safety check)
                if (!ttl.HasValue || ttl.Value.TotalSeconds <= 0)
                {
                    await database.KeyDeleteAsync(key);
                    expiredCount++;
                    _logger.LogDebug("Deleted expired session key: {Key}", key);
                }
                else
                {
                    activeCount++;
                }
            }

            _logger.LogInformation("Cleanup complete - Deleted: {ExpiredCount}, Active: {ActiveCount}",
                expiredCount, activeCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }
}
