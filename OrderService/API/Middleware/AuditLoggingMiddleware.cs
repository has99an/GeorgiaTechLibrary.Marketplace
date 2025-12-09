using System.Diagnostics;

namespace OrderService.API.Middleware;

/// <summary>
/// Middleware for audit logging of financial operations
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public AuditLoggingMiddleware(
        RequestDelegate next, 
        ILogger<AuditLoggingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
            var contentType = context.Request.ContentType;
            var contentLength = context.Request.ContentLength;
            
            _logger.LogInformation(
                "Audit: Starting {Method} {Path} from IP {IpAddress} Content-Type: {ContentType} ContentLength: {ContentLength}",
                method,
                path,
                context.Connection.RemoteIpAddress,
                contentType ?? "null",
                contentLength?.ToString() ?? "null");

            // Log request body for checkout requests in development
            if (path.Contains("/checkout", StringComparison.OrdinalIgnoreCase) && 
                _environment.IsDevelopment() && 
                method == "POST")
            {
                if (contentLength == null || contentLength == 0)
                {
                    _logger.LogError("CRITICAL: Checkout request body is EMPTY (ContentLength: {ContentLength}) - Request body not sent from UI!", 
                        contentLength?.ToString() ?? "null");
                }
                else if (contentLength > 0 && contentLength < 10240) // Only log if < 10KB
                {
                    try
                    {
                        context.Request.EnableBuffering();
                        context.Request.Body.Position = 0;
                        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                        var body = await reader.ReadToEndAsync();
                        _logger.LogInformation("Checkout request body: {Body}", body);
                        context.Request.Body.Position = 0; // Reset for model binding
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read request body for audit logging: {Error}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("Checkout request body too large to log ({ContentLength} bytes)", contentLength);
                }
            }
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

