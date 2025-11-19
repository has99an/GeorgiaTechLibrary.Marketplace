using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Email;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServiceDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();

        // Application Services
        services.AddScoped<INotificationService, Application.Services.NotificationService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();

        // Email Service (configurable)
        var emailProvider = configuration["Email:Provider"] ?? "Mock";
        if (emailProvider.Equals("SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, SendGridEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, MockEmailService>();
        }

        // Messaging
        services.AddHostedService<RabbitMQConsumer>();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck("rabbitmq", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("RabbitMQ is healthy"));

        return services;
    }
}

