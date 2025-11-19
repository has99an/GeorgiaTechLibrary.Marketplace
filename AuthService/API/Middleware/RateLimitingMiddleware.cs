using System.Collections.Concurrent;

namespace AuthService.API.Middleware;

/// <summary>
/// Rate limiting middleware for authentication endpoints
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    // In-memory storage for rate limiting
    private static readonly ConcurrentDictionary<string, RequestLog> _requestLogs = new();
    
    // Rate limit configurations
    private const int LoginLimitPerMinute = 5;
    private const int RegisterLimitPerHour = 3;
    private const int RefreshLimitPerMinute = 10;
    private const int ValidateLimitPerMinute = 100;

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
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private string GetEndpointKey(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        if (path.Contains("/login"))
            return "login";
        if (path.Contains("/register"))
            return "register";
        if (path.Contains("/refresh"))
            return "refresh";
        if (path.Contains("/validate"))
            return "validate";

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
                case "login":
                    var recentLogins = log.Requests.Count(r => r > now.AddMinutes(-1));
                    if (recentLogins >= LoginLimitPerMinute)
                    {
                        retryAfter = 60;
                        return true;
                    }
                    break;

                case "register":
                    var recentRegistrations = log.Requests.Count(r => r > now.AddHours(-1));
                    if (recentRegistrations >= RegisterLimitPerHour)
                    {
                        retryAfter = 3600;
                        return true;
                    }
                    break;

                case "refresh":
                    var recentRefreshes = log.Requests.Count(r => r > now.AddMinutes(-1));
                    if (recentRefreshes >= RefreshLimitPerMinute)
                    {
                        retryAfter = 60;
                        return true;
                    }
                    break;

                case "validate":
                    var recentValidations = log.Requests.Count(r => r > now.AddMinutes(-1));
                    if (recentValidations >= ValidateLimitPerMinute)
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

