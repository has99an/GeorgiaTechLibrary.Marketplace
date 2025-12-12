using AutoMapper;
using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces;
using UserService.Application.Mappings;
using UserService.Application.Services;
using UserService.Infrastructure.Messaging;
using UserService.Infrastructure.Persistence;

namespace UserService.API.Extensions;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISellerRepository, SellerRepository>();
        services.AddScoped<ISellerBookListingRepository, SellerBookListingRepository>();
        services.AddScoped<IBookSaleRepository, BookSaleRepository>();
        services.AddScoped<ISellerReviewRepository, SellerReviewRepository>();

        // Services
        services.AddScoped<IUserService, Application.Services.UserService>();
        services.AddScoped<ISellerService, SellerService>();

        // Messaging
        services.AddSingleton<IMessageProducer, RabbitMQProducer>();
        services.AddHostedService<RabbitMQConsumer>();
        services.AddHostedService<OrderEventConsumer>();
        
        // Note: Database migrations are now run synchronously in Program.cs before app starts
        // MigrationRunner is kept for reference but not used as hosted service

        // AutoMapper
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<SellerMappingProfile>();
        });
        var mapper = mapperConfig.CreateMapper();
        services.AddSingleton<IMapper>(mapper);

        // Health Checks
        var rabbitMQHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMQPort = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        var rabbitMQUsername = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMQPassword = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Service is running"), tags: new[] { "self" })
            .AddDbContextCheck<AppDbContext>("database", tags: new[] { "database" })
            .AddRabbitMQ(
                rabbitConnectionString: $"amqp://{rabbitMQUsername}:{rabbitMQPassword}@{rabbitMQHost}:{rabbitMQPort}/",
                name: "rabbitmq",
                timeout: TimeSpan.FromSeconds(3),
                tags: new[] { "rabbitmq" });

        return services;
    }

    public static IServiceCollection AddUserServiceCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("UserServicePolicy", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}

