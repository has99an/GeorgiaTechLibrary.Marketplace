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
    private const int UpdateUserLimitPerMinute = 20;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointKey(context);
        var key = $"{clientId}:{endpoint}";

        if (IsRateLimited(key, endpoint, out var retryAfter))
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientId}, Endpoint: {Endpoint}", 
                clientId, endpoint);
            
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        await _next(context);
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

    private bool IsRateLimited(string key, string endpoint, out int retryAfter)
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
                    if (recentUpdates >= UpdateUserLimitPerMinute)
                    {
                        retryAfter = 60;
                        return true;
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

