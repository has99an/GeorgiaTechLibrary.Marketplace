using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UserService.API.Extensions;
using UserService.API.Middleware;
using UserService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Disable automatic ModelState validation to allow manual handling
        options.SuppressModelStateInvalidFilter = false; // Keep automatic validation but allow manual override
        options.InvalidModelStateResponseFactory = context =>
        {
            // Return custom error response that works with Newtonsoft.Json
            var errors = new Dictionary<string, string[]>();
            foreach (var error in context.ModelState)
            {
                var errorMessages = error.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>();
                if (errorMessages.Length > 0)
                {
                    errors[error.Key] = errorMessages;
                }
            }
            
            var errorResponse = new UserService.Application.DTOs.ValidationErrorResponse
            {
                StatusCode = 400,
                Title = "Validation Error",
                Detail = "One or more validation errors occurred",
                Errors = errors
            };
            
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(errorResponse);
        };
    });

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with comprehensive documentation
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UserService API",
        Version = "v1",
        Description = "User profile management service for Georgia Tech Library Marketplace",
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

// Add UserService dependencies (Database, Repositories, Services, Messaging)
builder.Services.AddUserServiceDependencies(builder.Configuration);

// Add CORS
builder.Services.AddUserServiceCors(builder.Configuration);

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserService API v1");
        c.RoutePrefix = "swagger";
    });
}

// 5. HTTPS redirection
app.UseHttpsRedirection();

// 6. CORS
app.UseCors("UserServicePolicy");

// 7. Authorization
app.UseAuthorization();

// 8. Role-based authorization
app.UseMiddleware<RoleAuthorizationMiddleware>();

// 9. Map controllers and health checks
app.MapControllers();
app.MapHealthChecks("/health");

// Wait for SQL Server to be ready and seed data
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var retries = 10;

    while (retries > 0)
    {
        try
        {
            logger.LogInformation("Attempting database migration...");
            
            // Ensure delivery address columns exist (manual migration if needed)
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'DeliveryStreet')
                    BEGIN
                        ALTER TABLE Users ADD DeliveryStreet NVARCHAR(200) NULL;
                        ALTER TABLE Users ADD DeliveryCity NVARCHAR(100) NULL;
                        ALTER TABLE Users ADD DeliveryPostalCode NVARCHAR(10) NULL;
                        ALTER TABLE Users ADD DeliveryState NVARCHAR(100) NULL;
                        ALTER TABLE Users ADD DeliveryCountry NVARCHAR(100) NULL;
                    END
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'DeliveryState')
                    BEGIN
                        ALTER TABLE Users ADD DeliveryState NVARCHAR(100) NULL;
                    END
                ");
                logger.LogInformation("Delivery address columns ensured");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not ensure delivery address columns (may already exist)");
            }
            
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

app.Logger.LogInformation("UserService starting...");
await app.RunAsync();
