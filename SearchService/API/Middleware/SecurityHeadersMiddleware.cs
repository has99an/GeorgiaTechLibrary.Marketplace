namespace SearchService.API.Middleware;

/// <summary>
/// Middleware for adding security headers to all responses
/// Protects against common web vulnerabilities
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing request
        AddSecurityHeaders(context);

        await _next(context);

        // Remove sensitive headers after processing
        RemoveSensitiveHeaders(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent clickjacking attacks
        headers["X-Frame-Options"] = "DENY";

        // Prevent MIME-sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Enable XSS protection (legacy browsers)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Content Security Policy - restrict resource loading
        headers["Content-Security-Policy"] = "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        // Referrer Policy - control referrer information
        headers["Referrer-Policy"] = "no-referrer";

        // Permissions Policy - disable unnecessary browser features
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), " +
            "payment=(), usb=(), magnetometer=(), gyroscope=(), " +
            "accelerometer=(), ambient-light-sensor=()";

        // Strict-Transport-Security (HSTS) - enforce HTTPS
        // Only add in production with HTTPS
        if (!_environment.IsDevelopment() && context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Cross-Origin policies
        headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        // Cache control for sensitive endpoints
        if (context.Request.Path.StartsWithSegments("/api/search/stats") ||
            context.Request.Path.StartsWithSegments("/health"))
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }
        else
        {
            // Allow caching for search results
            headers["Cache-Control"] = "public, max-age=60";
        }
    }

    private void RemoveSensitiveHeaders(HttpContext context)
    {
        // Don't modify headers if response has already started
        if (context.Response.HasStarted)
        {
            return;
        }

        var headers = context.Response.Headers;

        // Remove server information headers
        headers.Remove("Server");
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");
        headers.Remove("X-AspNetMvc-Version");

        // Log if any sensitive information is being exposed
        if (headers.ContainsKey("X-SourceFiles"))
        {
            _logger.LogWarning("Sensitive header X-SourceFiles detected and removed");
            headers.Remove("X-SourceFiles");
        }
    }
}

