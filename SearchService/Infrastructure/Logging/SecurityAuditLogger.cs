using System.Text.Json;

namespace SearchService.Infrastructure.Logging;

/// <summary>
/// Specialized logger for security audit events
/// </summary>
public interface ISecurityAuditLogger
{
    Task LogSecurityEventAsync(SecurityEvent securityEvent);
    Task LogRateLimitViolationAsync(string clientIp, string endpoint, int requestCount);
    Task LogInvalidInputAttemptAsync(string clientIp, string endpoint, string inputType, string reason);
    Task LogSuspiciousActivityAsync(string clientIp, string activityType, string details);
}

public class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;

    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger)
    {
        _logger = logger;
    }

    public Task LogSecurityEventAsync(SecurityEvent securityEvent)
    {
        var eventJson = JsonSerializer.Serialize(securityEvent);
        
        _logger.LogWarning("SECURITY_EVENT: {EventType} | {EventJson}", 
            securityEvent.EventType, 
            eventJson);

        return Task.CompletedTask;
    }

    public Task LogRateLimitViolationAsync(string clientIp, string endpoint, int requestCount)
    {
        var securityEvent = new SecurityEvent
        {
            EventType = "RateLimitViolation",
            Timestamp = DateTime.UtcNow,
            ClientIp = AnonymizeIp(clientIp),
            Endpoint = endpoint,
            Details = new Dictionary<string, object>
            {
                { "RequestCount", requestCount }
            }
        };

        return LogSecurityEventAsync(securityEvent);
    }

    public Task LogInvalidInputAttemptAsync(string clientIp, string endpoint, string inputType, string reason)
    {
        var securityEvent = new SecurityEvent
        {
            EventType = "InvalidInputAttempt",
            Timestamp = DateTime.UtcNow,
            ClientIp = AnonymizeIp(clientIp),
            Endpoint = endpoint,
            Details = new Dictionary<string, object>
            {
                { "InputType", inputType },
                { "Reason", reason }
            }
        };

        return LogSecurityEventAsync(securityEvent);
    }

    public Task LogSuspiciousActivityAsync(string clientIp, string activityType, string details)
    {
        var securityEvent = new SecurityEvent
        {
            EventType = "SuspiciousActivity",
            Timestamp = DateTime.UtcNow,
            ClientIp = AnonymizeIp(clientIp),
            Details = new Dictionary<string, object>
            {
                { "ActivityType", activityType },
                { "Details", details }
            }
        };

        return LogSecurityEventAsync(securityEvent);
    }

    private string AnonymizeIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "unknown";

        // IPv4: Keep first 3 octets, mask last octet
        if (ip.Contains('.'))
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}.*";
            }
        }
        // IPv6: Keep first 4 groups, mask rest
        else if (ip.Contains(':'))
        {
            var parts = ip.Split(':');
            if (parts.Length >= 4)
            {
                return $"{parts[0]}:{parts[1]}:{parts[2]}:{parts[3]}::*";
            }
        }

        return "anonymized";
    }
}

public class SecurityEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ClientIp { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}

