using FluentValidation;
using MediatR;
using SearchService.Application.Common.Behaviors;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Mappings;
using SearchService.Domain.Services;
using SearchService.Infrastructure.Caching;
using SearchService.Infrastructure.Messaging.RabbitMQ;
using SearchService.Infrastructure.Persistence.Redis;
using StackExchange.Redis;
using System.Reflection;

namespace SearchService.API.Extensions;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application Layer services (MediatR, FluentValidation, Behaviors)
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.Queries.Books.SearchBooksQuery).Assembly);
        });

        // Add FluentValidation
        services.AddValidatorsFromAssembly(typeof(Application.Queries.Books.SearchBooksQuery).Assembly);

        // Add Pipeline Behaviors (order matters!)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SecurityAuditBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        // Add AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));

        // Add HttpContextAccessor for security audit
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Adds Infrastructure Layer services (Redis, RabbitMQ, Repositories)
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Redis
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var configOptions = ConfigurationOptions.Parse(redisConnectionString);
        configOptions.SyncTimeout = 10000;
        configOptions.AsyncTimeout = 10000;
        configOptions.ConnectTimeout = 10000;
        configOptions.AbortOnConnectFail = false;
        configOptions.KeepAlive = 60;

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(configOptions));

        // Add Repositories and Services
        services.AddScoped<IBookRepository, RedisBookRepository>();
        services.AddScoped<ISearchIndexService, RedisSearchIndexService>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddSingleton<ICachingStrategy, IntelligentCachingStrategy>();
        services.AddScoped<IAutocompleteService, Infrastructure.Search.RedisAutocompleteService>();
        services.AddScoped<IFacetIndexService, Infrastructure.Search.RedisFacetIndexService>();
        services.AddScoped<IFuzzySearchService, Infrastructure.Search.LevenshteinFuzzySearchService>();
        services.AddScoped<IAnalyticsRepository, RedisAnalyticsRepository>();

        // Add Security Services
        services.AddSingleton<Infrastructure.Logging.ISecurityAuditLogger, Infrastructure.Logging.SecurityAuditLogger>();
        services.AddScoped<Infrastructure.Security.IAnomalyDetector, Infrastructure.Security.AnomalyDetector>();

        // Add Background Services
        services.AddHostedService<BookEventConsumer>();

        return services;
    }

    /// <summary>
    /// Adds API Layer services (Controllers, Swagger, Health Checks)
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Controllers with filters
        services.AddControllers(options =>
        {
            // Add global query parameter validation filter
            options.Filters.Add<API.Filters.ValidateQueryParametersFilter>();
        });

        // Add Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "SearchService API - Clean Architecture",
                Version = "v2.0",
                Description = "Fast search functionality using Clean Architecture + CQRS pattern with MediatR. Provides book search, availability filtering, and seller information aggregation.",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "GeorgiaTechLibrary.Marketplace Team"
                }
            });

            // Add XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        // Add Health Checks
        services.AddHealthChecks()
            .AddCheck<Infrastructure.HealthChecks.RedisHealthCheck>("Redis", tags: new[] { "redis", "database" });

        return services;
    }
}

