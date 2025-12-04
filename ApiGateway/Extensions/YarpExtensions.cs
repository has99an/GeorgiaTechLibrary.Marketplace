using Yarp.ReverseProxy.Transforms;

namespace ApiGateway.Extensions;

public static class YarpExtensions
{
    public static IReverseProxyBuilder AddYarpConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builderContext =>
            {
                // Add custom headers to all requests
                builderContext.AddRequestTransform(async transformContext =>
                {
                    // Add gateway version header
                    transformContext.ProxyRequest.Headers.Add("X-Gateway-Version", "2.0");

                    // Forward real IP
                    var realIp = transformContext.HttpContext.Connection.RemoteIpAddress?.ToString();
                    if (!string.IsNullOrEmpty(realIp))
                    {
                        transformContext.ProxyRequest.Headers.Add("X-Forwarded-For", realIp);
                    }

                    // Add request ID if available
                    if (transformContext.HttpContext.Items.TryGetValue("RequestId", out var requestId))
                    {
                        transformContext.ProxyRequest.Headers.Add("X-Request-Id", requestId?.ToString() ?? string.Empty);
                    }

                    // Forward X-User-Id header if present (set by JwtAuthenticationMiddleware)
                    // Try Items first (more reliable), then headers
                    string? userId = null;
                    if (transformContext.HttpContext.Items.TryGetValue("X-User-Id", out var userIdFromItems))
                    {
                        userId = userIdFromItems?.ToString();
                    }
                    else if (transformContext.HttpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdFromHeaders))
                    {
                        // Get first value if multiple exist (avoid duplication)
                        userId = userIdFromHeaders.ToString().Split(',').FirstOrDefault()?.Trim();
                    }

                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Remove existing header first to avoid duplication, then set it
                        transformContext.ProxyRequest.Headers.Remove("X-User-Id");
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                    }

                    await Task.CompletedTask;
                });
            });
    }
}

