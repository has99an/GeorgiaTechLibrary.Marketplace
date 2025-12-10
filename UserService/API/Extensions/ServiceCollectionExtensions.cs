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
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

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

