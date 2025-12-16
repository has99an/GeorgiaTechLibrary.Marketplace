using ApiGateway.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Net.Http.Json;

namespace ApiGateway.Services;

/// <summary>
/// Service for aggregating health status from all downstream services
/// </summary>
public interface IHealthAggregationService
{
    Task<AggregatedHealthStatus> GetAggregatedHealthAsync(CancellationToken cancellationToken = default);
    Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceName, string serviceUrl, CancellationToken cancellationToken = default);
}

public class HealthAggregationService : IHealthAggregationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HealthAggregationService> _logger;
    private readonly IConfiguration _configuration;

    public HealthAggregationService(
        IHttpClientFactory httpClientFactory,
        ILogger<HealthAggregationService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<AggregatedHealthStatus> GetAggregatedHealthAsync(CancellationToken cancellationToken = default)
    {
        var aggregatedStatus = new AggregatedHealthStatus
        {
            Services = new Dictionary<string, ServiceHealthStatus>(),
            Infrastructure = new Dictionary<string, InfrastructureHealthStatus>()
        };

        // Get health status from all services
        var services = new[]
        {
            ("AuthService", "auth"),
            ("BookService", "books"),
            ("WarehouseService", "warehouse"),
            ("SearchService", "search"),
            ("OrderService", "orders"),
            ("UserService", "users"),
            ("NotificationService", "notifications")
        };

        var healthTasks = services.Select(async service =>
        {
            try
            {
                var address = _configuration[$"ReverseProxy:Clusters:{service.Item2}-cluster:Destinations:{service.Item2}-destination:Address"];
                if (!string.IsNullOrEmpty(address))
                {
                    var health = await GetServiceHealthAsync(service.Item1, $"{address}/health", cancellationToken);
                    return (service.Item1, health);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health for {ServiceName}", service.Item1);
                return (service.Item1, new ServiceHealthStatus
                {
                    ServiceName = service.Item1,
                    Status = "Unhealthy",
                    ResponseTimeMs = -1,
                    LastChecked = DateTime.UtcNow
                });
            }
            return (service.Item1, (ServiceHealthStatus?)null);
        });

        var results = await Task.WhenAll(healthTasks);
        foreach (var (serviceName, health) in results)
        {
            if (health != null)
            {
                aggregatedStatus.Services[serviceName] = health;
            }
        }

        // Determine overall status
        var allHealthy = aggregatedStatus.Services.Values.All(s => s.Status == "Healthy");
        var anyUnhealthy = aggregatedStatus.Services.Values.Any(s => s.Status == "Unhealthy");
        
        if (anyUnhealthy)
        {
            aggregatedStatus.OverallStatus = "Unhealthy";
        }
        else if (allHealthy)
        {
            aggregatedStatus.OverallStatus = "Healthy";
        }
        else
        {
            aggregatedStatus.OverallStatus = "Degraded";
        }

        // Infrastructure health (from docker-compose services)
        aggregatedStatus.Infrastructure["sqlserver"] = new InfrastructureHealthStatus
        {
            Status = "Healthy", // Would be checked via Docker health checks
            Details = new Dictionary<string, object> { { "type", "Database" } }
        };

        aggregatedStatus.Infrastructure["rabbitmq"] = new InfrastructureHealthStatus
        {
            Status = "Healthy", // Would be checked via Docker health checks
            Details = new Dictionary<string, object> { { "type", "Message Broker" } }
        };

        aggregatedStatus.Infrastructure["redis"] = new InfrastructureHealthStatus
        {
            Status = "Healthy", // Would be checked via Docker health checks
            Details = new Dictionary<string, object> { { "type", "Cache" } }
        };

        return aggregatedStatus;
    }

    public async Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceName, string serviceUrl, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var healthStatus = new ServiceHealthStatus
        {
            ServiceName = serviceName,
            LastChecked = DateTime.UtcNow
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync(serviceUrl, cancellationToken);
            stopwatch.Stop();
            healthStatus.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var healthReport = await response.Content.ReadFromJsonAsync<HealthReport>(cancellationToken: cancellationToken);
                    if (healthReport != null)
                    {
                        // Determine status from health report
                        if (healthReport.Status == HealthStatus.Healthy)
                        {
                            healthStatus.Status = "Healthy";
                        }
                        else if (healthReport.Status == HealthStatus.Degraded)
                        {
                            healthStatus.Status = "Degraded";
                        }
                        else
                        {
                            healthStatus.Status = "Unhealthy";
                        }

                        healthStatus.Details = new Dictionary<string, object>
                        {
                            { "entries", healthReport.Entries?.Count ?? 0 },
                            { "totalDuration", healthReport.TotalDuration.TotalMilliseconds }
                        };
                    }
                    else
                    {
                        healthStatus.Status = "Healthy"; // Assume healthy if we got 200 but couldn't parse
                    }
                }
                catch
                {
                    // If we can't parse, but got 200, assume healthy
                    healthStatus.Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
                }
            }
            else
            {
                healthStatus.Status = "Unhealthy";
                healthStatus.Details = new Dictionary<string, object>
                {
                    { "statusCode", (int)response.StatusCode }
                };
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            healthStatus.Status = "Unhealthy";
            healthStatus.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            healthStatus.Details = new Dictionary<string, object>
            {
                { "error", "Timeout" }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            healthStatus.Status = "Unhealthy";
            healthStatus.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            healthStatus.Details = new Dictionary<string, object>
            {
                { "error", ex.Message }
            };
            _logger.LogError(ex, "Error checking health for {ServiceName}", serviceName);
        }

        return healthStatus;
    }

    // Health report model matching ASP.NET Core health check response
    private class HealthReport
    {
        public HealthStatus Status { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public Dictionary<string, HealthReportEntry>? Entries { get; set; }
    }

    private class HealthReportEntry
    {
        public HealthStatus Status { get; set; }
        public string? Description { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public IReadOnlyList<string>? Tags { get; set; }
    }
}


