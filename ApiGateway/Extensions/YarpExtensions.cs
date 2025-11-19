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

                    await Task.CompletedTask;
                });
            });
    }
}

