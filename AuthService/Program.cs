using AuthService.API.Extensions;
using AuthService.API.Middleware;
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with comprehensive documentation
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1",
        Description = "Authentication and authorization service for Georgia Tech Library Marketplace",
        Contact = new OpenApiContact
        {
            Name = "Georgia Tech Library",
            Email = "library-admin@gatech.edu"
        }
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add AuthService dependencies (Database, Repositories, Services, Messaging)
builder.Services.AddAuthServiceDependencies(builder.Configuration);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline

// 1. Exception handling (must be first)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Audit logging
app.UseMiddleware<AuditLoggingMiddleware>();

// 3. Rate limiting
app.UseMiddleware<RateLimitingMiddleware>();

// 4. Swagger (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
        c.RoutePrefix = "swagger";
    });
}

// 5. HTTPS redirection
app.UseHttpsRedirection();

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Map controllers and health checks
app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = Microsoft.AspNetCore.Diagnostics.HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("rabbitmq"),
    ResponseWriter = Microsoft.AspNetCore.Diagnostics.HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self"),
    ResponseWriter = Microsoft.AspNetCore.Diagnostics.HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

// Wait for SQL Server to be ready and seed data
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var retries = 30; // More retries for Docker startup

    while (retries > 0)
    {
        try
        {
            logger.LogInformation("Attempting database migration...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migration successful. Starting seed data...");
            await SeedData.InitializeAsync(dbContext, logger);
            logger.LogInformation("Seed data completed successfully");
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database not ready. Retries left: {Retries}", retries);
            retries--;
            if (retries > 0)
            {
                await Task.Delay(5000);
            }
            else
            {
                logger.LogError("Failed to initialize database after all retries");
            }
        }
    }
}

app.Logger.LogInformation("AuthService starting...");
await app.RunAsync();
