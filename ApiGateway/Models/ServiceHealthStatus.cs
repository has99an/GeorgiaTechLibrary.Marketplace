namespace ApiGateway.Models;

/// <summary>
/// Health status for a single service
/// </summary>
public class ServiceHealthStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Healthy, Degraded, Unhealthy
    public long ResponseTimeMs { get; set; }
    public DateTime LastChecked { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Aggregated health status for all services
/// </summary>
public class AggregatedHealthStatus
{
    public string OverallStatus { get; set; } = string.Empty; // Healthy, Degraded, Unhealthy
    public Dictionary<string, ServiceHealthStatus> Services { get; set; } = new();
    public Dictionary<string, InfrastructureHealthStatus> Infrastructure { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health status for infrastructure components
/// </summary>
public class InfrastructureHealthStatus
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object>? Details { get; set; }
}



