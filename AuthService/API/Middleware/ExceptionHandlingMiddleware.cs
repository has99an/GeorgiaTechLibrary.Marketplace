using System.Net;
using System.Text.Json;
using AuthService.Domain.Exceptions;

namespace AuthService.API.Middleware;

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
            case InvalidCredentialsException:
                statusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = "Invalid email or password",
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

            case AuthenticationException authEx:
                statusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = authEx.Message,
                    Errors = (object?)null
                };
                break;

            case DomainException domainEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                errorResponse = new
                {
                    StatusCode = statusCode,
                    Message = domainEx.Message,
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
}

