namespace SearchService.Application.Common.Interfaces;

/// <summary>
/// Interface for intelligent caching strategies
/// </summary>
public interface ICachingStrategy
{
    /// <summary>
    /// Determines the TTL (Time To Live) for a cache entry based on query type and frequency
    /// </summary>
    /// <param name="queryType">The type of query being cached</param>
    /// <param name="cacheKey">The cache key for tracking</param>
    /// <returns>TimeSpan representing the cache duration</returns>
    TimeSpan GetTtl(string queryType, string cacheKey);

    /// <summary>
    /// Records a cache hit for adaptive learning
    /// </summary>
    Task RecordCacheHitAsync(string queryType, string cacheKey);

    /// <summary>
    /// Records a cache miss for adaptive learning
    /// </summary>
    Task RecordCacheMissAsync(string queryType, string cacheKey);

    /// <summary>
    /// Gets the cache hit rate for a specific query type
    /// </summary>
    Task<double> GetCacheHitRateAsync(string queryType);

    /// <summary>
    /// Determines if cache warming should be performed for this query
    /// </summary>
    bool ShouldWarmCache(string queryType, string cacheKey);
}

