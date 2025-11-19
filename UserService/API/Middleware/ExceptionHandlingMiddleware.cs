using System.Net;
using System.Text.Json;
using UserService.Domain.Exceptions;

namespace UserService.API.Middleware;

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

            case UserNotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = SanitizeErrorMessage(notFoundEx.Message),
                    Errors = (object?)null
                };
                break;

            case DuplicateEmailException duplicateEx:
                statusCode = (int)HttpStatusCode.Conflict;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = "A user with this email already exists",
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
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = "An internal server error occurred",
                    Errors = (object?)null
                };
                break;
        }

        response.StatusCode = statusCode;
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    /// <summary>
    /// Sanitizes error messages to prevent information leakage
    /// </summary>
    private string SanitizeErrorMessage(string message)
    {
        // Remove any potential sensitive information
        // In production, you might want to be more aggressive here
        return message;
    }
}

