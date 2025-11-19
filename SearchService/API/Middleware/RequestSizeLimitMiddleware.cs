namespace SearchService.API.Middleware;

/// <summary>
/// Middleware for limiting request sizes to prevent DoS attacks
/// </summary>
public class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSizeLimitMiddleware> _logger;

    private const int MaxRequestBodySize = 1_048_576; // 1 MB
    private const int MaxQueryStringLength = 2048; // 2 KB
    private const int MaxHeaderSize = 8192; // 8 KB

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        ILogger<RequestSizeLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check query string length
        if (context.Request.QueryString.HasValue && 
            context.Request.QueryString.Value.Length > MaxQueryStringLength)
        {
            _logger.LogWarning("Query string too long: {Length} bytes from {IP}", 
                context.Request.QueryString.Value.Length,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 414; // URI Too Long
            await context.Response.WriteAsJsonAsync(new
            {
                StatusCode = 414,
                Message = $"Query string too long. Maximum {MaxQueryStringLength} characters allowed."
            });
            return;
        }

        // Check header size
        var totalHeaderSize = context.Request.Headers.Sum(h => h.Key.Length + (h.Value.ToString()?.Length ?? 0));
        if (totalHeaderSize > MaxHeaderSize)
        {
            _logger.LogWarning("Headers too large: {Size} bytes from {IP}", 
                totalHeaderSize,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 431; // Request Header Fields Too Large
            await context.Response.WriteAsJsonAsync(new
            {
                StatusCode = 431,
                Message = "Request headers too large."
            });
            return;
        }

        // Check request body size (for POST/PUT requests)
        if (context.Request.ContentLength.HasValue && 
            context.Request.ContentLength.Value > MaxRequestBodySize)
        {
            _logger.LogWarning("Request body too large: {Size} bytes from {IP}", 
                context.Request.ContentLength.Value,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 413; // Payload Too Large
            await context.Response.WriteAsJsonAsync(new
            {
                StatusCode = 413,
                Message = $"Request body too large. Maximum {MaxRequestBodySize / 1024 / 1024} MB allowed."
            });
            return;
        }

        await _next(context);
    }
}

