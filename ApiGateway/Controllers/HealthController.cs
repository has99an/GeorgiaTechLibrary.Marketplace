using ApiGateway.Models;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>
/// Health monitoring endpoints for aggregated service health
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IHealthAggregationService _healthAggregationService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthAggregationService healthAggregationService,
        ILogger<HealthController> logger)
    {
        _healthAggregationService = healthAggregationService;
        _logger = logger;
    }

    /// <summary>
    /// Get aggregated health status for all services
    /// </summary>
    [HttpGet("aggregated")]
    [ProducesResponseType(typeof(AggregatedHealthStatus), 200)]
    public async Task<ActionResult<AggregatedHealthStatus>> GetAggregatedHealth()
    {
        try
        {
            var health = await _healthAggregationService.GetAggregatedHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aggregated health");
            return StatusCode(500, new { error = "Failed to get health status" });
        }
    }

    /// <summary>
    /// Get detailed health status for all services
    /// </summary>
    [HttpGet("services")]
    [ProducesResponseType(typeof(Dictionary<string, ServiceHealthStatus>), 200)]
    public async Task<ActionResult<Dictionary<string, ServiceHealthStatus>>> GetServicesHealth()
    {
        try
        {
            var aggregated = await _healthAggregationService.GetAggregatedHealthAsync();
            return Ok(aggregated.Services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services health");
            return StatusCode(500, new { error = "Failed to get services health" });
        }
    }

    /// <summary>
    /// Get health metrics (response times, error rates, etc.)
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetHealthMetrics()
    {
        try
        {
            var aggregated = await _healthAggregationService.GetAggregatedHealthAsync();
            
            var metrics = new
            {
                overallStatus = aggregated.OverallStatus,
                totalServices = aggregated.Services.Count,
                healthyServices = aggregated.Services.Values.Count(s => s.Status == "Healthy"),
                degradedServices = aggregated.Services.Values.Count(s => s.Status == "Degraded"),
                unhealthyServices = aggregated.Services.Values.Count(s => s.Status == "Unhealthy"),
                averageResponseTime = aggregated.Services.Values
                    .Where(s => s.ResponseTimeMs > 0)
                    .DefaultIfEmpty(new ServiceHealthStatus { ResponseTimeMs = 0 })
                    .Average(s => s.ResponseTimeMs),
                services = aggregated.Services.Values.Select(s => new
                {
                    name = s.ServiceName,
                    status = s.Status,
                    responseTimeMs = s.ResponseTimeMs
                }),
                timestamp = aggregated.Timestamp
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health metrics");
            return StatusCode(500, new { error = "Failed to get health metrics" });
        }
    }
}


