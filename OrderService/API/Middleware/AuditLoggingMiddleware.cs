using System.Diagnostics;

namespace OrderService.API.Middleware;

/// <summary>
/// Middleware for audit logging of financial operations
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // Check if this is a financial operation that should be audited
        var shouldAudit = ShouldAuditRequest(path, method);

        if (shouldAudit)
        {
            _logger.LogInformation(
                "Audit: Starting {Method} {Path} from IP {IpAddress}",
                method,
                path,
                context.Connection.RemoteIpAddress);
        }

        await _next(context);

        stopwatch.Stop();

        if (shouldAudit)
        {
            _logger.LogInformation(
                "Audit: Completed {Method} {Path} with status {StatusCode} in {ElapsedMs}ms",
                method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool ShouldAuditRequest(string path, string method)
    {
        // Audit all POST, PUT, DELETE operations
        if (method is "POST" or "PUT" or "DELETE")
            return true;

        // Audit specific financial endpoints
        var auditPaths = new[]
        {
            "/api/orders",
            "/api/shoppingcart",
            "/pay",
            "/refund",
            "/cancel"
        };

        return auditPaths.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}

