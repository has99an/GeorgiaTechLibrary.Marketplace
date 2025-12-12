using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Infrastructure.Messaging;
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Extensions;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServiceDependencies(
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
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();

        // Services
        services.AddScoped<IAuthService, Application.Services.AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // Messaging
        services.AddSingleton<IMessageProducer, RabbitMQProducer>();
        services.AddHostedService<UserEventConsumer>();

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
}

