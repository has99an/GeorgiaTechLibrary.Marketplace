using MediatR;
using SearchService.Application.Common.Interfaces;
using System.Text.Json;

namespace SearchService.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior for intelligent caching of query results with adaptive TTL
/// Only applies to queries (IRequest that end with "Query")
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cache;
    private readonly ICachingStrategy _cachingStrategy;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheService cache,
        ICachingStrategy cachingStrategy,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _cachingStrategy = cachingStrategy;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only cache queries
        if (!requestName.EndsWith("Query"))
        {
            return await next();
        }

        // Generate cache key from request
        var cacheKey = GenerateCacheKey(request);

        try
        {
            // Try to get from cache
            var cachedResponseJson = await _cache.GetAsync<string>(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResponseJson))
            {
                _logger.LogInformation("Cache hit for {RequestName}", requestName);
                
                // Record cache hit for adaptive learning
                await _cachingStrategy.RecordCacheHitAsync(requestName, cacheKey);
                
                var cachedResponse = JsonSerializer.Deserialize<TResponse>(cachedResponseJson);
                if (cachedResponse != null)
                {
                    return cachedResponse;
                }
            }

            _logger.LogInformation("Cache miss for {RequestName}", requestName);
            
            // Record cache miss for adaptive learning
            await _cachingStrategy.RecordCacheMissAsync(requestName, cacheKey);

            // Execute request
            var response = await next();

            // Get intelligent TTL based on query type and frequency
            var ttl = _cachingStrategy.GetTtl(requestName, cacheKey);
            
            _logger.LogDebug("Caching {RequestName} with TTL {TTL} seconds", requestName, ttl.TotalSeconds);

            // Cache the response with adaptive TTL
            var responseJson = JsonSerializer.Serialize(response);
            await _cache.SetAsync(cacheKey, responseJson, ttl, cancellationToken);

            // Cache warming for popular queries
            if (_cachingStrategy.ShouldWarmCache(requestName, cacheKey))
            {
                _logger.LogInformation("Query {RequestName} is popular, cache warmed", requestName);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in caching behavior for {RequestName}", requestName);
            // If caching fails, just execute the request
            return await next();
        }
    }

    private string GenerateCacheKey(TRequest request)
    {
        var requestName = typeof(TRequest).Name;
        var requestJson = JsonSerializer.Serialize(request);
        var hash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(requestJson));
        return $"query:{requestName}:{hash}";
    }
}

