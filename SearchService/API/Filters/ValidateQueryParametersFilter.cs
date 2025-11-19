using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SearchService.Application.Common.Validators;

namespace SearchService.API.Filters;

/// <summary>
/// Action filter to validate query parameters and reject unexpected ones
/// Prevents parameter pollution attacks
/// </summary>
public class ValidateQueryParametersFilter : IActionFilter
{
    private readonly ILogger<ValidateQueryParametersFilter> _logger;

    // Whitelist of allowed query parameters per endpoint
    private static readonly Dictionary<string, HashSet<string>> AllowedParameters = new()
    {
        { "SearchBooks", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "query", "page", "pageSize", "sortBy" } },
        { "GetAvailableBooks", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "page", "pageSize", "sortBy", "sortOrder" } },
        { "GetBookByIsbn", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "isbn" } },
        { "GetBookSellers", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "isbn" } },
        { "GetAutocomplete", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "prefix", "maxResults" } },
        { "GetFacets", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "searchTerm" } },
        { "GetPopularSearches", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "topN", "timeWindow" } },
        { "GetStats", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { } }
    };

    public ValidateQueryParametersFilter(ILogger<ValidateQueryParametersFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var actionName = context.ActionDescriptor.RouteValues["action"];
        
        if (string.IsNullOrEmpty(actionName))
            return;

        // Get allowed parameters for this endpoint
        if (!AllowedParameters.TryGetValue(actionName, out var allowedParams))
        {
            // If endpoint not in whitelist, allow all (for flexibility)
            return;
        }

        // Check for unexpected query parameters
        var queryParams = context.HttpContext.Request.Query.Keys;
        var unexpectedParams = queryParams.Where(p => !allowedParams.Contains(p)).ToList();

        if (unexpectedParams.Any())
        {
            _logger.LogWarning("Unexpected query parameters detected in {Action}: {Parameters} from {IP}",
                actionName,
                string.Join(", ", unexpectedParams),
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new BadRequestObjectResult(new
            {
                StatusCode = 400,
                Message = "Invalid query parameters detected",
                InvalidParameters = unexpectedParams,
                AllowedParameters = allowedParams.ToList()
            });
        }

        // Check for suspicious parameter values
        foreach (var param in queryParams)
        {
            var value = context.HttpContext.Request.Query[param].ToString();
            
            if (InputSanitizer.ContainsSuspiciousPatterns(value))
            {
                _logger.LogWarning("Suspicious pattern in query parameter {Parameter} from {IP}",
                    param,
                    context.HttpContext.Connection.RemoteIpAddress);

                context.Result = new BadRequestObjectResult(new
                {
                    StatusCode = 400,
                    Message = "Invalid characters detected in query parameters"
                });
                return;
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No action needed after execution
    }
}

