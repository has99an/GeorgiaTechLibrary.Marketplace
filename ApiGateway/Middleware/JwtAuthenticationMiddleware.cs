using ApiGateway.Services;

namespace ApiGateway.Middleware;

public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;

    public JwtAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenValidationService tokenValidationService)
    {
        var path = context.Request.Path.Value?.ToLower();

        // Allow certain paths without authentication
        if (IsPathAllowedWithoutAuth(path, context))
        {
            await _next(context);
            return;
        }

        // Check for Authorization header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            _logger.LogWarning("Request to {Path} missing Authorization header", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authorization header missing");
            return;
        }

        // Extract token from "Bearer <token>"
        var authHeaderValue = authHeader.ToString();
        if (!authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid Authorization header format for path {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid Authorization header format");
            return;
        }

        var token = authHeaderValue.Substring("Bearer ".Length).Trim();

        // Validate token with AuthService
        if (!await tokenValidationService.ValidateTokenAsync(token))
        {
            _logger.LogWarning("Token validation failed for path {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
            return;
        }

        // Extract UserId from token and add to headers
        var userId = tokenValidationService.ExtractUserIdFromToken(token);
        if (!string.IsNullOrEmpty(userId))
        {
            context.Request.Headers["X-User-Id"] = userId;
        }

        await _next(context);
    }

    private bool IsPathAllowedWithoutAuth(string? path, HttpContext context)
    {
        if (string.IsNullOrEmpty(path)) return false;

        // Allow auth endpoints
        if (path.StartsWith("/auth/")) return true;

        // Allow health checks
        if (path == "/health") return true;

        // Allow swagger
        if (path.StartsWith("/swagger")) return true;

        // Allow GET requests to books endpoints (public access)
        if ((path.StartsWith("/books/") || path == "/books") && context.Request.Method == "GET")
        {
            return true;
        }

        // Allow GET requests to search endpoints (public access)
        if ((path.StartsWith("/search/") || path == "/search") && context.Request.Method == "GET")
        {
            return true;
        }

        return false;
    }

}
