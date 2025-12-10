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
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

        return services;
    }
}

