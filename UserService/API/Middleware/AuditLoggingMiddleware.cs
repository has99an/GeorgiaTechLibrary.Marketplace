using System.Diagnostics;
using System.Text.Json;

namespace UserService.API.Middleware;

/// <summary>
/// Middleware for auditing user actions
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only audit mutating operations
        if (ShouldAudit(context.Request))
        {
            await AuditRequestAsync(context);
        }
        else
        {
            await _next(context);
        }
    }

    private bool ShouldAudit(HttpRequest request)
    {
        var method = request.Method.ToUpper();
        var path = request.Path.Value?.ToLower() ?? string.Empty;

        // Audit POST, PUT, DELETE operations on user endpoints
        if ((method == "POST" || method == "PUT" || method == "DELETE") &&
            path.Contains("/api/users"))
        {
            return true;
        }

        return false;
    }

    private async Task AuditRequestAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        var originalBodyStream = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Extract user information
            var userId = context.Request.Headers["X-User-Id"].FirstOrDefault() ?? "Anonymous";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var correlationId = context.TraceIdentifier;

            // Execute the request
            await _next(context);

            stopwatch.Stop();

            // Log audit information
            var auditLog = new
            {
                CorrelationId = correlationId,
                UserId = userId,
                IpAddress = ipAddress,
                Method = request.Method,
                Path = request.Path.Value,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("AUDIT: {AuditLog}", JsonSerializer.Serialize(auditLog));

            // Reset position to beginning
            responseBody.Seek(0, SeekOrigin.Begin);
            
            // Update Content-Length header if it exists
            if (context.Response.ContentLength.HasValue)
            {
                context.Response.ContentLength = responseBody.Length;
            }
            
            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}

