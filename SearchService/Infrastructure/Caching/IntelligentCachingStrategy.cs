using SearchService.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Caching;

/// <summary>
/// Intelligent caching strategy with adaptive TTL based on query patterns
/// </summary>
public class IntelligentCachingStrategy : ICachingStrategy
{
    private readonly IDatabase _database;
    private readonly ILogger<IntelligentCachingStrategy> _logger;

    // Default TTL values based on data volatility
    private static readonly Dictionary<string, TimeSpan> DefaultTtls = new()
    {
        // Hot data - popular searches
        { "SearchBooksQuery", TimeSpan.FromMinutes(15) },
        { "GetAutocompleteQuery", TimeSpan.FromMinutes(15) },
        { "GetPopularSearchesQuery", TimeSpan.FromMinutes(15) },
        
        // Warm data - available books and facets
        { "GetAvailableBooksQuery", TimeSpan.FromMinutes(10) },
        { "GetSearchFacetsQuery", TimeSpan.FromMinutes(10) },
        { "SearchBooksWithFiltersQuery", TimeSpan.FromMinutes(10) },
        
        // Cold data - specific book details
        { "GetBookByIsbnQuery", TimeSpan.FromMinutes(5) },
        { "GetBookSellersQuery", TimeSpan.FromMinutes(5) },
        { "GetFeaturedBooksQuery", TimeSpan.FromMinutes(5) },
        
        // Stats and analytics - frequently changing
        { "GetSearchStatsQuery", TimeSpan.FromMinutes(2) },
        { "GetSearchAnalyticsQuery", TimeSpan.FromMinutes(2) }
    };

    // Popularity thresholds for cache warming
    private const int WarmCacheThreshold = 10; // Warm cache if accessed 10+ times in last hour

    public IntelligentCachingStrategy(
        IConnectionMultiplexer redis,
        ILogger<IntelligentCachingStrategy> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public TimeSpan GetTtl(string queryType, string cacheKey)
    {
        try
        {
            // Get base TTL for query type
            var baseTtl = DefaultTtls.TryGetValue(queryType, out var ttl) 
                ? ttl 
                : TimeSpan.FromMinutes(5);

            // Check query frequency for adaptive TTL
            var frequencyKey = $"cache:frequency:{queryType}:{GetShortHash(cacheKey)}";
            var frequency = (int)_database.StringGet(frequencyKey);

            // Increase TTL for frequently accessed queries
            if (frequency > 50) // Very hot
            {
                return TimeSpan.FromMinutes(baseTtl.TotalMinutes * 2);
            }
            else if (frequency > 20) // Hot
            {
                return TimeSpan.FromMinutes(baseTtl.TotalMinutes * 1.5);
            }

            return baseTtl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating TTL for {QueryType}", queryType);
            return TimeSpan.FromMinutes(5); // Safe default
        }
    }

    public async Task RecordCacheHitAsync(string queryType, string cacheKey)
    {
        try
        {
            var hitKey = $"cache:hits:{queryType}";
            var frequencyKey = $"cache:frequency:{queryType}:{GetShortHash(cacheKey)}";
            
            // Increment hit counter
            await _database.StringIncrementAsync(hitKey);
            await _database.KeyExpireAsync(hitKey, TimeSpan.FromHours(24));
            
            // Track query frequency
            await _database.StringIncrementAsync(frequencyKey);
            await _database.KeyExpireAsync(frequencyKey, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cache hit for {QueryType}", queryType);
        }
    }

    public async Task RecordCacheMissAsync(string queryType, string cacheKey)
    {
        try
        {
            var missKey = $"cache:misses:{queryType}";
            var frequencyKey = $"cache:frequency:{queryType}:{GetShortHash(cacheKey)}";
            
            // Increment miss counter
            await _database.StringIncrementAsync(missKey);
            await _database.KeyExpireAsync(missKey, TimeSpan.FromHours(24));
            
            // Track query frequency
            await _database.StringIncrementAsync(frequencyKey);
            await _database.KeyExpireAsync(frequencyKey, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cache miss for {QueryType}", queryType);
        }
    }

    public async Task<double> GetCacheHitRateAsync(string queryType)
    {
        try
        {
            var hitKey = $"cache:hits:{queryType}";
            var missKey = $"cache:misses:{queryType}";
            
            var hits = (long)await _database.StringGetAsync(hitKey);
            var misses = (long)await _database.StringGetAsync(missKey);
            
            var total = hits + misses;
            if (total == 0) return 0;
            
            return (double)hits / total * 100;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating cache hit rate for {QueryType}", queryType);
            return 0;
        }
    }

    public bool ShouldWarmCache(string queryType, string cacheKey)
    {
        try
        {
            var frequencyKey = $"cache:frequency:{queryType}:{GetShortHash(cacheKey)}";
            var frequency = (int)_database.StringGet(frequencyKey);
            
            return frequency >= WarmCacheThreshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache warming for {QueryType}", queryType);
            return false;
        }
    }

    private string GetShortHash(string input)
    {
        // Create a short hash for frequency tracking (to avoid key explosion)
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash)[..8]; // Use first 8 chars
    }
}

