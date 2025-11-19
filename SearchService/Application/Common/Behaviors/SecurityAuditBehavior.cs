using MediatR;
using Microsoft.AspNetCore.Http;
using SearchService.Infrastructure.Logging;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior for security auditing and suspicious pattern detection
/// </summary>
public class SecurityAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ISecurityAuditLogger _securityLogger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SecurityAuditBehavior<TRequest, TResponse>> _logger;

    public SecurityAuditBehavior(
        ISecurityAuditLogger securityLogger,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SecurityAuditBehavior<TRequest, TResponse>> logger)
    {
        _securityLogger = securityLogger;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var endpoint = httpContext.Request.Path.ToString();

            // Check for suspicious patterns in request
            await DetectSuspiciousPatterns(request, clientIp, endpoint);
        }

        return await next();
    }

    private async Task DetectSuspiciousPatterns(TRequest request, string clientIp, string endpoint)
    {
        // Use reflection to check string properties for suspicious patterns
        var properties = typeof(TRequest).GetProperties()
            .Where(p => p.PropertyType == typeof(string));

        foreach (var property in properties)
        {
            var value = property.GetValue(request) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (InputSanitizer.ContainsSuspiciousPatterns(value))
                {
                    _logger.LogWarning("Suspicious pattern detected in {PropertyName} from {ClientIp}", 
                        property.Name, clientIp);

                    await _securityLogger.LogInvalidInputAttemptAsync(
                        clientIp, 
                        endpoint, 
                        property.Name, 
                        "Suspicious pattern detected");
                }

                // Check for potential enumeration attempts (sequential patterns)
                if (property.Name.Contains("ISBN", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsSequentialPattern(value))
                    {
                        await _securityLogger.LogSuspiciousActivityAsync(
                            clientIp, 
                            "PotentialEnumeration", 
                            $"Sequential ISBN pattern detected: {property.Name}");
                    }
                }
            }
        }
    }

    private bool IsSequentialPattern(string value)
    {
        // Simple check for sequential numbers (e.g., 1234567890, 0000000001, etc.)
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        
        if (digitsOnly.Length < 5)
            return false;

        // Check for all same digits
        if (digitsOnly.All(c => c == digitsOnly[0]))
            return true;

        // Check for sequential ascending/descending
        bool isSequential = true;
        for (int i = 1; i < Math.Min(digitsOnly.Length, 6); i++)
        {
            if (digitsOnly[i] != digitsOnly[i - 1] + 1 && digitsOnly[i] != digitsOnly[i - 1] - 1)
            {
                isSequential = false;
                break;
            }
        }

        return isSequential;
    }
}

