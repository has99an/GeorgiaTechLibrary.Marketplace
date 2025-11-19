using System.Diagnostics;
using System.Text.Json;

namespace AuthService.API.Middleware;

/// <summary>
/// Middleware for auditing authentication events
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
        // Only audit authentication operations
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
        var path = request.Path.Value?.ToLower() ?? string.Empty;
        
        // Audit login, register, and token operations
        return path.Contains("/register") || 
               path.Contains("/login") || 
               path.Contains("/refresh");
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

            // Extract information
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var correlationId = context.TraceIdentifier;

            // Execute the request
            await _next(context);

            stopwatch.Stop();

            // Log audit information
            var auditLog = new
            {
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                Method = request.Method,
                Path = request.Path.Value,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                Success = context.Response.StatusCode < 400
            };

            var logLevel = context.Response.StatusCode < 400 ? LogLevel.Information : LogLevel.Warning;
            _logger.Log(logLevel, "AUTH_AUDIT: {AuditLog}", JsonSerializer.Serialize(auditLog));

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}

