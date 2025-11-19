using StackExchange.Redis;
using System.Net;

namespace SearchService.API.Middleware;

/// <summary>
/// Middleware for rate limiting using Redis
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private const int MaxRequestsPerMinute = 100;
    private const int MaxRequestsPerHour = 1000;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var database = redis.GetDatabase();

        try
        {
            // Check rate limits
            var minuteKey = $"ratelimit:{clientIp}:minute";
            var hourKey = $"ratelimit:{clientIp}:hour";

            // Increment counters
            var minuteCount = await database.StringIncrementAsync(minuteKey);
            var hourCount = await database.StringIncrementAsync(hourKey);

            // Set expiry on first request
            if (minuteCount == 1)
            {
                await database.KeyExpireAsync(minuteKey, TimeSpan.FromMinutes(1));
            }

            if (hourCount == 1)
            {
                await database.KeyExpireAsync(hourKey, TimeSpan.FromHours(1));
            }

            // Check limits
            if (minuteCount > MaxRequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded for IP {ClientIp}: {Count} requests/minute",
                    clientIp, minuteCount);

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsJsonAsync(new
                {
                    StatusCode = 429,
                    Message = "Rate limit exceeded. Maximum 100 requests per minute allowed.",
                    RetryAfter = "60 seconds"
                });
                return;
            }

            if (hourCount > MaxRequestsPerHour)
            {
                _logger.LogWarning("Hourly rate limit exceeded for IP {ClientIp}: {Count} requests/hour",
                    clientIp, hourCount);

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = "3600";
                await context.Response.WriteAsJsonAsync(new
                {
                    StatusCode = 429,
                    Message = "Rate limit exceeded. Maximum 1000 requests per hour allowed.",
                    RetryAfter = "3600 seconds"
                });
                return;
            }

            // Add rate limit headers
            context.Response.Headers["X-RateLimit-Limit-Minute"] = MaxRequestsPerMinute.ToString();
            context.Response.Headers["X-RateLimit-Remaining-Minute"] = (MaxRequestsPerMinute - minuteCount).ToString();
            context.Response.Headers["X-RateLimit-Limit-Hour"] = MaxRequestsPerHour.ToString();
            context.Response.Headers["X-RateLimit-Remaining-Hour"] = (MaxRequestsPerHour - hourCount).ToString();

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware");
            // Continue without rate limiting if Redis fails
            await _next(context);
        }
    }
}

