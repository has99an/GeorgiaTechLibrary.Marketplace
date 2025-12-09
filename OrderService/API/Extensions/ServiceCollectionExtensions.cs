using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Payment;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Services;

namespace OrderService.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderServiceDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();

        // Application Services
        services.AddScoped<IOrderService, Application.Services.OrderService>();
        
        // Payment Service (must be registered before ShoppingCartService)
        var paymentProvider = configuration["Payment:Provider"] ?? "Mock";
        if (paymentProvider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPaymentService, StripePaymentService>();
        }
        else
        {
            services.AddScoped<IPaymentService, MockPaymentService>();
        }
        
        services.AddScoped<IShoppingCartService, ShoppingCartService>();

        // Infrastructure Services
        services.AddScoped<IInventoryService, InventoryService>();

        // Note: UserService HTTP Client removed - all communication via messaging

        // Messaging
        services.AddSingleton<IMessageProducer, RabbitMQProducer>();
        services.AddHostedService<RabbitMQConsumer>();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck("rabbitmq", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("RabbitMQ is healthy"));

        return services;
    }
}

