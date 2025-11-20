using SearchService.Domain.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace SearchService.API.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        
        // Don't modify response if it has already started
        if (response.HasStarted)
        {
            _logger.LogWarning("Cannot handle exception - response has already started");
            return;
        }

        response.ContentType = "application/json";

        object errorResponse;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = "Validation failed",
                    Errors = validationEx.Errors
                };
                break;

            case NotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = SanitizeErrorMessage(notFoundEx.Message),
                    Errors = (object?)null
                };
                break;

            case DomainException domainEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = SanitizeErrorMessage(domainEx.Message),
                    Errors = (object?)null
                };
                break;

            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                // Don't expose internal error details in production
                var message = "An internal server error occurred. Please try again later.";
                
                // Log full exception details server-side
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = message,
                    Errors = (object?)null
                };
                break;
        }

        response.StatusCode = statusCode;
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private string SanitizeErrorMessage(string message)
    {
        // Remove any potential sensitive information from error messages
        // such as file paths, connection strings, or internal details
        
        // Remove file paths
        message = System.Text.RegularExpressions.Regex.Replace(message, @"[A-Za-z]:\\[\w\\\.\-]+", "[path]");
        message = System.Text.RegularExpressions.Regex.Replace(message, @"/[\w/\.\-]+", "[path]");
        
        // Remove connection strings patterns
        message = System.Text.RegularExpressions.Regex.Replace(message, @"(Server|Data Source|Password|Pwd)=[^;]+", "[redacted]", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return message;
    }
}

