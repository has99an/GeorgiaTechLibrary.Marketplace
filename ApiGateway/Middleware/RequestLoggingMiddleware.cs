using System.Diagnostics;

namespace ApiGateway.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();

        // Add request ID to context for tracking
        context.Items["RequestId"] = requestId;

        // Log request with Content-Type and ContentLength for POST/PUT/PATCH
        if (context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            var contentType = context.Request.ContentType;
            var contentLength = context.Request.ContentLength;
            
            _logger.LogInformation(
                "Request started: {RequestId} {Method} {Path} {QueryString} Content-Type: {ContentType} ContentLength: {ContentLength}",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                contentType ?? "null",
                contentLength?.ToString() ?? "null");

            // Enable buffering to allow reading body multiple times
            context.Request.EnableBuffering();

            // Log request body in development mode for debugging
            if (_environment.IsDevelopment())
            {
                if (contentLength == null || contentLength == 0)
                {
                    _logger.LogWarning("Request body is EMPTY (ContentLength: {ContentLength}) for {RequestId} - UI may not be sending request body!", 
                        contentLength?.ToString() ?? "null", requestId);
                }
                else if (contentLength > 0 && contentLength < 10240) // Only log if < 10KB
                {
                    try
                    {
                        context.Request.Body.Position = 0;
                        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                        var body = await reader.ReadToEndAsync();
                        _logger.LogInformation("Request body for {RequestId}: {Body}", requestId, body);
                        context.Request.Body.Position = 0; // Reset for next middleware
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read request body for {RequestId}: {Error}", requestId, ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("Request body too large to log ({ContentLength} bytes) for {RequestId}", contentLength, requestId);
                }
            }
        }
        else
        {
            _logger.LogInformation(
                "Request started: {RequestId} {Method} {Path} {QueryString}",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            _logger.LogInformation(
                "Request completed: {RequestId} {Method} {Path} {StatusCode} {Duration}ms",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

