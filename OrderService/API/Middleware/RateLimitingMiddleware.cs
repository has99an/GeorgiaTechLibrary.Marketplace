using System.Collections.Concurrent;
using System.Net;

namespace OrderService.API.Middleware;

/// <summary>
/// Rate limiting middleware to prevent abuse
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimits = new();

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = GetEndpointKey(context);
        var clientId = GetClientIdentifier(context);
        var key = $"{clientId}:{endpoint}";

        var limit = GetLimitForEndpoint(endpoint);
        var window = GetWindowForEndpoint(endpoint);

        if (!IsRequestAllowed(key, limit, window))
        {
            _logger.LogWarning("Rate limit exceeded for {ClientId} on {Endpoint}", clientId, endpoint);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Rate limit exceeded. Please try again later.\"}");
            return;
        }

        await _next(context);
    }

    private bool IsRequestAllowed(string key, int limit, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var rateLimitInfo = _rateLimits.GetOrAdd(key, _ => new RateLimitInfo());

        lock (rateLimitInfo)
        {
            // Remove expired requests
            rateLimitInfo.Requests.RemoveAll(r => now - r > window);

            // Check if limit is exceeded
            if (rateLimitInfo.Requests.Count >= limit)
                return false;

            // Add current request
            rateLimitInfo.Requests.Add(now);
            return true;
        }
    }

    private string GetEndpointKey(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // Normalize endpoint keys
        if (path.Contains("/api/orders") && method == "POST" && !path.Contains("/pay"))
            return "order:create";
        if (path.Contains("/pay") && method == "POST")
            return "order:pay";
        if (path.Contains("/api/shoppingcart") && method == "POST")
            return "cart:add";
        if (path.Contains("/checkout") && method == "POST")
            return "cart:checkout";

        return $"{method}:{path}";
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get customer ID from route or query
        if (context.Request.RouteValues.TryGetValue("customerId", out var customerId))
            return customerId?.ToString() ?? "anonymous";

        // Fall back to IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private int GetLimitForEndpoint(string endpoint)
    {
        return endpoint switch
        {
            "order:create" => 10,      // 10 orders per minute
            "order:pay" => 5,          // 5 payments per minute
            "cart:add" => 30,          // 30 cart additions per minute
            "cart:checkout" => 5,      // 5 checkouts per minute
            _ => 100                   // Default: 100 requests per minute
        };
    }

    private TimeSpan GetWindowForEndpoint(string endpoint)
    {
        return endpoint switch
        {
            "order:pay" => TimeSpan.FromMinutes(1),
            "cart:checkout" => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private class RateLimitInfo
    {
        public List<DateTime> Requests { get; } = new();
    }
}

