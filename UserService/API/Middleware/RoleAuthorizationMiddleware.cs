using UserService.Application.Interfaces;
using UserService.Domain.ValueObjects;

namespace UserService.API.Middleware;

/// <summary>
/// Middleware for role-based authorization
/// </summary>
public class RoleAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RoleAuthorizationMiddleware> _logger;

    public RoleAuthorizationMiddleware(
        RequestDelegate next,
        ILogger<RoleAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        var method = context.Request.Method.ToUpper();

        // Skip authorization for health checks and non-user endpoints
        if (!path.Contains("/api/users") || path == "/health")
        {
            await _next(context);
            return;
        }

        // Extract user ID from header (set by ApiGateway)
        var userIdHeader = context.Request.Headers["X-User-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userIdHeader))
        {
            // Allow GET requests without authentication for public data
            if (method == "GET")
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("Unauthorized request to {Path}: Missing X-User-Id header", path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            _logger.LogWarning("Invalid X-User-Id header: {UserId}", userIdHeader);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid user ID");
            return;
        }

        // Get user to check role
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("User not found");
            return;
        }

        // Check permissions based on endpoint and method
        if (!await HasPermissionAsync(context, user.Role, userId))
        {
            _logger.LogWarning("Forbidden: User {UserId} with role {Role} attempted {Method} {Path}",
                userId, user.Role, method, path);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Forbidden: Insufficient permissions");
            return;
        }

        // Store user info in context for controllers
        context.Items["UserId"] = userId;
        context.Items["UserRole"] = user.Role;

        await _next(context);
    }

    private Task<bool> HasPermissionAsync(HttpContext context, UserRole role, Guid userId)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        var method = context.Request.Method.ToUpper();

        // Admin can do everything
        if (role == UserRole.Admin)
        {
            return Task.FromResult(true);
        }

        // DELETE operations require admin
        if (method == "DELETE")
        {
            return Task.FromResult(false);
        }

        // Role changes require admin
        if (method == "PUT" && path.Contains("/role"))
        {
            return Task.FromResult(false);
        }

        // Users can update their own profile
        if (method == "PUT" && path.Contains($"/api/users/{userId}"))
        {
            return Task.FromResult(true);
        }

        // Sellers can update their own profile
        if (method == "PUT" && role == UserRole.Seller)
        {
            // Check if updating own profile
            var pathSegments = path.Split('/');
            if (pathSegments.Length >= 4 && Guid.TryParse(pathSegments[3], out var targetUserId))
            {
                return Task.FromResult(targetUserId == userId);
            }
        }

        // Students can only read
        if (method == "GET")
        {
            return Task.FromResult(true);
        }

        // POST (create) is allowed for authenticated users (handled by ApiGateway)
        if (method == "POST")
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

