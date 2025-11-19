using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SearchService.Infrastructure.HealthChecks;

/// <summary>
/// Custom health check for Redis connection and index health
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IConnectionMultiplexer redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            
            // Check Redis connectivity
            var pingTime = await database.PingAsync();
            
            if (pingTime.TotalMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Redis is slow. Ping time: {pingTime.TotalMilliseconds}ms",
                    data: new Dictionary<string, object>
                    {
                        { "PingTimeMs", pingTime.TotalMilliseconds },
                        { "Status", "Degraded" }
                    });
            }

            // Check index health
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var bookCount = 0;
            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 100))
            {
                bookCount++;
                if (bookCount >= 100) break; // Sample only
            }

            // Check cache hit rate
            var cacheHitRate = await GetCacheHitRateAsync(database);

            return HealthCheckResult.Healthy(
                "Redis is healthy",
                data: new Dictionary<string, object>
                {
                    { "PingTimeMs", pingTime.TotalMilliseconds },
                    { "SampleBookCount", bookCount },
                    { "CacheHitRate", $"{cacheHitRate:F2}%" },
                    { "Status", "Healthy" }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy(
                "Redis is unhealthy",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    { "Error", ex.Message },
                    { "Status", "Unhealthy" }
                });
        }
    }

    private async Task<double> GetCacheHitRateAsync(IDatabase database)
    {
        try
        {
            var hits = (long)await database.StringGetAsync("cache:hits:SearchBooksQuery");
            var misses = (long)await database.StringGetAsync("cache:misses:SearchBooksQuery");
            var total = hits + misses;
            
            return total > 0 ? (double)hits / total * 100 : 0;
        }
        catch
        {
            return 0;
        }
    }
}

