using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Caching;
using OrderService.Infrastructure.Jobs;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Payment;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Services;
using StackExchange.Redis;

namespace OrderService.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderServiceDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Redis
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.SyncTimeout = 10000;
            configOptions.AsyncTimeout = 10000;
            configOptions.ConnectTimeout = 10000;
            configOptions.AbortOnConnectFail = false;
            configOptions.KeepAlive = 60;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
        services.AddScoped<IPaymentAllocationRepository, PaymentAllocationRepository>();
        services.AddScoped<ISellerSettlementRepository, SellerSettlementRepository>();

        // Caching Services
        services.AddScoped<IRedisCacheService, RedisCacheService>();

        // Application Services
        services.AddScoped<IOrderService, Application.Services.OrderService>();
        services.AddScoped<IPaymentAllocationService, PaymentAllocationService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        
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

        // Background Jobs
        services.AddHostedService<CleanupExpiredSessionsJob>();
        services.AddHostedService<PaymentSettlementJob>();

        // Note: UserService HTTP Client removed - all communication via messaging

        // Messaging
        services.AddSingleton<IMessageProducer, RabbitMQProducer>();
        services.AddHostedService<RabbitMQConsumer>();

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

