using ApiGateway.Configuration;
using ApiGateway.Services;
using ApiGateway.Models;

namespace ApiGateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiGatewayServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<SecuritySettings>(configuration.GetSection("Security"));

        // Memory Cache
        services.AddMemoryCache();

        // HTTP Clients
        services.AddHttpClient<ITokenValidationService, TokenValidationService>();
        services.AddHttpClient<ISwaggerAggregationService, SwaggerAggregationService>();
        services.AddHttpClient<IHealthAggregationService, HealthAggregationService>();

        // Services
        services.AddSingleton<ISwaggerAggregationService, SwaggerAggregationService>();
        services.AddScoped<IHealthAggregationService, HealthAggregationService>();

        // Response Compression
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        // CORS
        var securitySettings = configuration.GetSection("Security").Get<SecuritySettings>();
        if (securitySettings?.Cors != null)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("ApiGatewayPolicy", builder =>
                {
                    var corsSettings = securitySettings.Cors;
                    
                    if (corsSettings.AllowedOrigins.Length > 0)
                    {
                        builder.WithOrigins(corsSettings.AllowedOrigins);
                    }
                    else
                    {
                        builder.AllowAnyOrigin();
                    }

                    if (corsSettings.AllowedMethods.Length > 0)
                    {
                        // Always include OPTIONS for CORS preflight
                        var methods = corsSettings.AllowedMethods.ToList();
                        if (!methods.Contains("OPTIONS"))
                        {
                            methods.Add("OPTIONS");
                        }
                        builder.WithMethods(methods.ToArray());
                    }
                    else
                    {
                        builder.AllowAnyMethod();
                    }

                    if (corsSettings.AllowedHeaders.Length > 0)
                    {
                        builder.WithHeaders(corsSettings.AllowedHeaders);
                    }
                    else
                    {
                        builder.AllowAnyHeader();
                    }

                    if (corsSettings.AllowCredentials)
                    {
                        builder.AllowCredentials();
                    }
                });
            });
        }

        return services;
    }

    public static IServiceCollection AddApiGatewayHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks();

        // Add health checks for all downstream services
        var services_list = new[]
        {
            ("auth", "AuthService"),
            ("books", "BookService"),
            ("warehouse", "WarehouseService"),
            ("search", "SearchService"),
            ("orders", "OrderService"),
            ("users", "UserService"),
            ("notifications", "NotificationService")
        };

        foreach (var (clusterKey, serviceName) in services_list)
        {
            var address = configuration[$"ReverseProxy:Clusters:{clusterKey}-cluster:Destinations:{clusterKey}-destination:Address"];
            if (!string.IsNullOrEmpty(address))
            {
                healthChecks.AddUrlGroup(
                    new Uri($"{address}/health"),
                    name: serviceName,
                    timeout: TimeSpan.FromSeconds(5));
            }
        }

        return services;
    }
}

