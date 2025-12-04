using System.Collections.Concurrent;

namespace UserService.API.Middleware;

/// <summary>
/// Simple in-memory rate limiting middleware
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    // In-memory storage for rate limiting (use Redis in production)
    private static readonly ConcurrentDictionary<string, RequestLog> _requestLogs = new();
    
    // Rate limit configurations
    private const int GeneralLimitPerMinute = 100;
    private const int CreateUserLimitPerHour = 5;
    private const int UpdateUserLimitPerMinute = 200; // Increased significantly for UI development/testing

    private readonly IWebHostEnvironment _environment;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting in development if configured
        if (_environment.IsDevelopment())
        {
            // In development, use much higher limits or skip entirely
            var clientId = GetClientIdentifier(context);
            var endpoint = GetEndpointKey(context);
            var key = $"{clientId}:{endpoint}";

            // Log for debugging but don't block
            if (IsRateLimited(key, endpoint, out var retryAfter, isDevelopment: true))
            {
                _logger.LogWarning("Rate limit would be exceeded (dev mode - allowing): {ClientId}, Endpoint: {Endpoint}, Count: {Count}", 
                    clientId, endpoint, GetRequestCount(key));
            }
        }
        else
        {
            var clientId = GetClientIdentifier(context);
            var endpoint = GetEndpointKey(context);
            var key = $"{clientId}:{endpoint}";

            if (IsRateLimited(key, endpoint, out var retryAfter))
            {
                _logger.LogWarning("Rate limit exceeded for client: {ClientId}, Endpoint: {Endpoint}, Count: {Count}", 
                    clientId, endpoint, GetRequestCount(key));
                
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
                return;
            }
        }

        await _next(context);
    }

    private int GetRequestCount(string key)
    {
        if (_requestLogs.TryGetValue(key, out var log))
        {
            lock (log)
            {
                var now = DateTime.UtcNow;
                return log.Requests.Count(r => r > now.AddMinutes(-1));
            }
        }
        return 0;
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID from header, otherwise use IP address
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private string GetEndpointKey(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        if (method == "POST" && path.Contains("/api/users") && !path.Contains("/"))
        {
            return "create_user";
        }
        
        if (method == "PUT" && path.Contains("/api/users/"))
        {
            return "update_user";
        }

        return "general";
    }

    private bool IsRateLimited(string key, string endpoint, out int retryAfter, bool isDevelopment = false)
    {
        retryAfter = 60;
        var now = DateTime.UtcNow;

        // Get or create request log
        var log = _requestLogs.GetOrAdd(key, _ => new RequestLog());

        lock (log)
        {
            // Clean old entries
            log.Requests.RemoveAll(r => r < now.AddHours(-1));

            // Check rate limits based on endpoint
            switch (endpoint)
            {
                case "create_user":
                    var recentCreates = log.Requests.Count(r => r > now.AddHours(-1));
                    if (recentCreates >= CreateUserLimitPerHour)
                    {
                        retryAfter = 3600; // 1 hour
                        return true;
                    }
                    break;

                case "update_user":
                    var recentUpdates = log.Requests.Count(r => r > now.AddMinutes(-1));
                    // In development, use 10x higher limit
                    var effectiveLimit = isDevelopment ? UpdateUserLimitPerMinute * 10 : UpdateUserLimitPerMinute;
                    
                    // Log for debugging
                    if (recentUpdates > 0 && recentUpdates % 10 == 0)
                    {
                        _logger.LogInformation("Rate limit check for {Key}: {Count} requests in last minute (limit: {Limit}, dev: {IsDev})", 
                            key, recentUpdates, effectiveLimit, isDevelopment);
                    }
                    if (recentUpdates >= effectiveLimit)
                    {
                        if (!isDevelopment)
                        {
                            _logger.LogWarning("Rate limit EXCEEDED for {Key}: {Count} requests in last minute (limit: {Limit})", 
                                key, recentUpdates, effectiveLimit);
                        }
                        retryAfter = 60;
                        return !isDevelopment; // Only block in production
                    }
                    break;

                default:
                    var recentGeneral = log.Requests.Count(r => r > now.AddMinutes(-1));
                    if (recentGeneral >= GeneralLimitPerMinute)
                    {
                        retryAfter = 60;
                        return true;
                    }
                    break;
            }

            // Add current request
            log.Requests.Add(now);
        }

        return false;
    }

    private class RequestLog
    {
        public List<DateTime> Requests { get; } = new();
    }
}

