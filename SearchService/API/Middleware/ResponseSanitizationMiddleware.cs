namespace SearchService.API.Middleware;

/// <summary>
/// Middleware for sanitizing responses and adding appropriate cache headers
/// </summary>
public class ResponseSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseSanitizationMiddleware> _logger;

    public ResponseSanitizationMiddleware(
        RequestDelegate next,
        ILogger<ResponseSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Skip header modification if response has already started or for health checks
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (context.Response.HasStarted || path == "/health" || path == "/health/ready" || path.StartsWith("/health/"))
        {
            return;
        }

        // Add appropriate cache-control headers based on endpoint
        AddCacheControlHeaders(context);

        // Remove any sensitive headers that might have been added
        RemoveSensitiveResponseHeaders(context);
    }

    private void AddCacheControlHeaders(HttpContext context)
    {
        var path = context.Request.Path.ToString().ToLowerInvariant();

        // Don't cache error responses
        if (context.Response.StatusCode >= 400)
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            return;
        }

        // Set cache headers based on endpoint type
        if (path.Contains("/search") && !path.Contains("/stats") && !path.Contains("/popular"))
        {
            // Cache search results for 1 minute
            context.Response.Headers["Cache-Control"] = "public, max-age=60, stale-while-revalidate=30";
        }
        else if (path.Contains("/autocomplete"))
        {
            // Cache autocomplete for 5 minutes (changes less frequently)
            context.Response.Headers["Cache-Control"] = "public, max-age=300, stale-while-revalidate=60";
        }
        else if (path.Contains("/facets"))
        {
            // Cache facets for 10 minutes
            context.Response.Headers["Cache-Control"] = "public, max-age=600, stale-while-revalidate=120";
        }
        else if (path.Contains("/available") || path.Contains("/featured"))
        {
            // Cache available/featured books for 2 minutes
            context.Response.Headers["Cache-Control"] = "public, max-age=120, stale-while-revalidate=30";
        }
        else if (path.Contains("/stats") || path.Contains("/popular"))
        {
            // Don't cache analytics/stats
            context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
        }
        else
        {
            // Default: short cache
            context.Response.Headers["Cache-Control"] = "public, max-age=30";
        }
    }

    private void RemoveSensitiveResponseHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Remove headers that might expose internal information
        var sensitiveHeaders = new[]
        {
            "X-AspNet-Version",
            "X-AspNetMvc-Version",
            "X-Powered-By",
            "Server",
            "X-SourceFiles"
        };

        foreach (var header in sensitiveHeaders)
        {
            if (headers.ContainsKey(header))
            {
                headers.Remove(header);
            }
        }
    }
}

