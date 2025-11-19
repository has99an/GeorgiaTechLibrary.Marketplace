using Microsoft.Extensions.Caching.Memory;

namespace ApiGateway.Services;

public class SwaggerAggregationService : ISwaggerAggregationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SwaggerAggregationService> _logger;
    private readonly IMemoryCache _cache;

    private readonly Dictionary<string, string> _serviceClusterMap = new()
    {
        { "auth", "auth-cluster" },
        { "books", "books-cluster" },
        { "warehouse", "warehouse-cluster" },
        { "search", "search-cluster" },
        { "orders", "orders-cluster" },
        { "users", "users-cluster" }
    };

    public SwaggerAggregationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SwaggerAggregationService> logger,
        IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public async Task<string?> GetSwaggerDocumentAsync(string serviceName)
    {
        if (!_serviceClusterMap.TryGetValue(serviceName.ToLower(), out var clusterName))
        {
            _logger.LogWarning("Unknown service name: {ServiceName}", serviceName);
            return null;
        }

        // Check cache first
        var cacheKey = $"swagger_{serviceName}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedDocument))
        {
            _logger.LogDebug("Swagger document for {ServiceName} retrieved from cache", serviceName);
            return cachedDocument;
        }

        try
        {
            var baseUrl = _configuration[$"ReverseProxy:Clusters:{clusterName}:Destinations:{serviceName}-destination:Address"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogError("No address configured for service {ServiceName}", serviceName);
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            var swaggerUrl = $"{baseUrl}/swagger/v1/swagger.json";
            
            _logger.LogInformation("Fetching Swagger document from {Url}", swaggerUrl);
            var response = await client.GetStringAsync(swaggerUrl);

            // Cache for 5 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, response, cacheOptions);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Swagger document for service {ServiceName}", serviceName);
            return null;
        }
    }

    public IEnumerable<string> GetAvailableServices()
    {
        return _serviceClusterMap.Keys;
    }
}

