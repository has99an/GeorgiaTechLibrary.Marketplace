using StackExchange.Redis;
using System.Text.Json;

namespace OrderService.Infrastructure.Caching;

/// <summary>
/// Redis-based cache service for storing checkout sessions and other temporary data
/// </summary>
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached value for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, serializedValue, expiry);
            
            _logger.LogDebug("Cached value for key: {Key}, Expiry: {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching value for key {Key}", key);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            var result = await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Deleted cache key: {Key}, Success: {Success}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cache key {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of cache key {Key}", key);
            return false;
        }
    }
}
