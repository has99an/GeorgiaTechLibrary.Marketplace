using StackExchange.Redis;
using SearchService.Infrastructure.Logging;

namespace SearchService.Infrastructure.Security;

/// <summary>
/// Detects anomalous behavior patterns that may indicate attacks or abuse
/// </summary>
public interface IAnomalyDetector
{
    Task<bool> IsAnomalousActivityAsync(string clientIp, string activityType);
    Task RecordActivityAsync(string clientIp, string activityType);
    Task<bool> IsIpBlockedAsync(string clientIp);
    Task BlockIpTemporarilyAsync(string clientIp, TimeSpan duration);
}

public class AnomalyDetector : IAnomalyDetector
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISecurityAuditLogger _securityLogger;
    private readonly ILogger<AnomalyDetector> _logger;
    private readonly IDatabase _database;

    // Thresholds
    private const int RapidSearchThreshold = 20; // 20 searches in 10 seconds
    private const int SequentialIsbnThreshold = 5; // 5 sequential ISBNs in 1 minute
    private const int ZeroResultThreshold = 10; // 10 zero-result searches in 5 minutes

    public AnomalyDetector(
        IConnectionMultiplexer redis,
        ISecurityAuditLogger securityLogger,
        ILogger<AnomalyDetector> logger)
    {
        _redis = redis;
        _securityLogger = securityLogger;
        _logger = logger;
        _database = redis.GetDatabase();
    }

    public async Task<bool> IsAnomalousActivityAsync(string clientIp, string activityType)
    {
        var key = $"anomaly:{clientIp}:{activityType}";
        var count = await _database.StringGetAsync(key);

        if (count.HasValue)
        {
            var activityCount = (int)count;

            // Check different thresholds based on activity type
            return activityType switch
            {
                "RapidSearch" => activityCount > RapidSearchThreshold,
                "SequentialIsbn" => activityCount > SequentialIsbnThreshold,
                "ZeroResults" => activityCount > ZeroResultThreshold,
                _ => false
            };
        }

        return false;
    }

    public async Task RecordActivityAsync(string clientIp, string activityType)
    {
        var key = $"anomaly:{clientIp}:{activityType}";
        var ttl = GetTtlForActivityType(activityType);

        var count = await _database.StringIncrementAsync(key);
        
        if (count == 1)
        {
            await _database.KeyExpireAsync(key, ttl);
        }

        // Check if this activity is now anomalous
        if (await IsAnomalousActivityAsync(clientIp, activityType))
        {
            _logger.LogWarning("Anomalous activity detected: {ActivityType} from {ClientIp} (count: {Count})",
                activityType, clientIp, count);

            await _securityLogger.LogSuspiciousActivityAsync(
                clientIp,
                activityType,
                $"Threshold exceeded: {count} activities");

            // Auto-block IP temporarily
            await BlockIpTemporarilyAsync(clientIp, TimeSpan.FromMinutes(15));
        }
    }

    public async Task<bool> IsIpBlockedAsync(string clientIp)
    {
        var key = $"blocked:ip:{clientIp}";
        return await _database.KeyExistsAsync(key);
    }

    public async Task BlockIpTemporarilyAsync(string clientIp, TimeSpan duration)
    {
        var key = $"blocked:ip:{clientIp}";
        await _database.StringSetAsync(key, DateTime.UtcNow.ToString("O"), duration);

        _logger.LogWarning("IP {ClientIp} temporarily blocked for {Duration} minutes",
            clientIp, duration.TotalMinutes);

        await _securityLogger.LogSecurityEventAsync(new SecurityEvent
        {
            EventType = "IpBlocked",
            Timestamp = DateTime.UtcNow,
            ClientIp = clientIp,
            Details = new Dictionary<string, object>
            {
                { "Duration", duration.ToString() },
                { "Reason", "Anomalous activity detected" }
            }
        });
    }

    private TimeSpan GetTtlForActivityType(string activityType)
    {
        return activityType switch
        {
            "RapidSearch" => TimeSpan.FromSeconds(10),
            "SequentialIsbn" => TimeSpan.FromMinutes(1),
            "ZeroResults" => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(1)
        };
    }
}

