using System.Collections.Concurrent;
using ApiGateway.Configuration;
using Microsoft.Extensions.Options;

namespace ApiGateway.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitSettings _settings;
    private readonly ConcurrentDictionary<string, ClientRateLimit> _clients = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<SecuritySettings> securitySettings)
    {
        _next = next;
        _logger = logger;
        _settings = securitySettings.Value.RateLimit;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var clientId = GetClientId(context);
        var endpoint = GetEndpoint(context);

        var rateLimit = GetRateLimitForEndpoint(endpoint);
        var clientRateLimit = _clients.GetOrAdd(clientId, _ => new ClientRateLimit());

        if (!clientRateLimit.AllowRequest(endpoint, rateLimit.Limit, rateLimit.PeriodInSeconds))
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} on endpoint {Endpoint}",
                clientId,
                endpoint);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = rateLimit.PeriodInSeconds.ToString();
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        await _next(context);
    }

    private string GetClientId(HttpContext context)
    {
        // Try to get client IP
        var ip = context.Connection.RemoteIpAddress?.ToString();
        
        // If behind proxy, try X-Forwarded-For
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
        }

        return ip ?? "unknown";
    }

    private string GetEndpoint(HttpContext context)
    {
        return $"{context.Request.Method}:{context.Request.Path}";
    }

    private (int Limit, int PeriodInSeconds) GetRateLimitForEndpoint(string endpoint)
    {
        // Check for specific endpoint limits
        foreach (var (pattern, limit) in _settings.EndpointLimits)
        {
            if (endpoint.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return (limit.Limit, limit.PeriodInSeconds);
            }
        }

        // Return general limit
        return (_settings.GeneralLimit, _settings.GeneralPeriodInSeconds);
    }
}

public class ClientRateLimit
{
    private readonly ConcurrentDictionary<string, EndpointRequests> _endpoints = new();

    public bool AllowRequest(string endpoint, int limit, int periodInSeconds)
    {
        var endpointRequests = _endpoints.GetOrAdd(endpoint, _ => new EndpointRequests());
        return endpointRequests.AllowRequest(limit, periodInSeconds);
    }
}

public class EndpointRequests
{
    private readonly object _lock = new();
    private Queue<DateTime> _requests = new();

    public bool AllowRequest(int limit, int periodInSeconds)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddSeconds(-periodInSeconds);

            // Remove old requests outside the window
            while (_requests.Count > 0 && _requests.Peek() < windowStart)
            {
                _requests.Dequeue();
            }

            // Check if limit is exceeded
            if (_requests.Count >= limit)
            {
                return false;
            }

            // Add current request
            _requests.Enqueue(now);
            return true;
        }
    }
}

