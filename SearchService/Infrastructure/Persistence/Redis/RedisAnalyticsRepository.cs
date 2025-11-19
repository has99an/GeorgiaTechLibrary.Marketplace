using SearchService.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Persistence.Redis;

/// <summary>
/// Redis implementation of IAnalyticsRepository
/// </summary>
public class RedisAnalyticsRepository : IAnalyticsRepository
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisAnalyticsRepository> _logger;

    public RedisAnalyticsRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisAnalyticsRepository> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task TrackSearchAsync(string searchTerm, int resultCount, long responseTimeMs)
    {
        try
        {
            var termLower = searchTerm.ToLowerInvariant();
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            // Increment search count in sorted set (for popularity)
            tasks.Add(batch.SortedSetIncrementAsync("analytics:searches", termLower, 1));
            tasks.Add(batch.SortedSetIncrementAsync("analytics:searches:daily", termLower, 1));

            // Track zero-result searches separately
            if (resultCount == 0)
            {
                tasks.Add(batch.SortedSetIncrementAsync("analytics:zero-results", termLower, 1));
            }

            // Store response time
            var responseKey = $"analytics:response:{termLower}";
            tasks.Add(batch.ListRightPushAsync(responseKey, responseTimeMs));
            tasks.Add(batch.ListTrimAsync(responseKey, -100, -1)); // Keep last 100 measurements

            // Store result count
            var resultKey = $"analytics:results:{termLower}";
            tasks.Add(batch.StringSetAsync(resultKey, resultCount));

            // Set expiry on daily stats
            tasks.Add(batch.KeyExpireAsync("analytics:searches:daily", TimeSpan.FromDays(1)));

            batch.Execute();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking search: {SearchTerm}", searchTerm);
        }
    }

    public async Task TrackClickThroughAsync(string searchTerm, string isbn)
    {
        try
        {
            var termLower = searchTerm.ToLowerInvariant();
            var clickKey = $"analytics:clicks:{termLower}";
            
            await _database.SortedSetIncrementAsync(clickKey, isbn, 1);
            await _database.KeyExpireAsync(clickKey, TimeSpan.FromDays(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking click-through: {SearchTerm} -> {ISBN}", searchTerm, isbn);
        }
    }

    public async Task<IEnumerable<(string SearchTerm, int Count)>> GetPopularSearchesAsync(int topN = 10, string timeWindow = "24h")
    {
        try
        {
            var key = timeWindow == "24h" ? "analytics:searches:daily" : "analytics:searches";
            var results = await _database.SortedSetRangeByRankWithScoresAsync(key, 0, topN - 1, Order.Descending);
            
            return results.Select(r => (r.Element.ToString(), (int)r.Score));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular searches");
            return Enumerable.Empty<(string, int)>();
        }
    }

    public async Task<IEnumerable<(string SearchTerm, int Count)>> GetZeroResultSearchesAsync(int topN = 10)
    {
        try
        {
            var results = await _database.SortedSetRangeByRankWithScoresAsync(
                "analytics:zero-results", 0, topN - 1, Order.Descending);
            
            return results.Select(r => (r.Element.ToString(), (int)r.Score));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting zero-result searches");
            return Enumerable.Empty<(string, int)>();
        }
    }

    public async Task<Dictionary<string, object>> GetSearchStatisticsAsync()
    {
        try
        {
            var stats = new Dictionary<string, object>();

            // Total unique searches
            var totalSearches = await _database.SortedSetLengthAsync("analytics:searches");
            stats["TotalUniqueSearches"] = totalSearches;

            // Total search count
            var allSearches = await _database.SortedSetRangeByRankWithScoresAsync("analytics:searches", 0, -1);
            var totalSearchCount = allSearches.Sum(s => s.Score);
            stats["TotalSearchCount"] = (int)totalSearchCount;

            // Zero-result searches
            var zeroResults = await _database.SortedSetLengthAsync("analytics:zero-results");
            stats["ZeroResultSearches"] = zeroResults;

            // Average response time (simplified)
            stats["AverageResponseTimeMs"] = 0; // Would need to calculate from stored values

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search statistics");
            return new Dictionary<string, object>();
        }
    }
}

