using Microsoft.EntityFrameworkCore;
using OrderService.API.Extensions;
using OrderService.API.Middleware;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Custom validation error response
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var error in context.ModelState)
            {
                var errorMessages = error.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>();
                if (errorMessages.Length > 0)
                {
                    errors[error.Key] = errorMessages;
                }
            }
            
            var errorResponse = new
            {
                error = "Validation failed",
                errors = errors,
                statusCode = 400
            };
            
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(errorResponse);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OrderService API",
        Version = "v1",
        Description = "Order and Shopping Cart Management API with Clean Architecture"
    });
});

// Add OrderService dependencies (Clean Architecture)
builder.Services.AddOrderServiceDependencies(builder.Configuration);

// Add logging
builder.Services.AddLogging();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("rabbitmq"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Database migration and seeding
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var retries = 10;

    while (retries > 0)
    {
        try
        {
            // Ensure all delivery address columns exist (fallback to migrations)
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DeliveryStreet')
                    BEGIN
                        ALTER TABLE Orders ADD DeliveryStreet NVARCHAR(200) NOT NULL DEFAULT '';
                    END
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DeliveryCity')
                    BEGIN
                        ALTER TABLE Orders ADD DeliveryCity NVARCHAR(100) NOT NULL DEFAULT '';
                    END
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DeliveryPostalCode')
                    BEGIN
                        ALTER TABLE Orders ADD DeliveryPostalCode NVARCHAR(10) NOT NULL DEFAULT '';
                    END
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DeliveryState')
                    BEGIN
                        ALTER TABLE Orders ADD DeliveryState NVARCHAR(100) NULL;
                    END
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DeliveryCountry')
                    BEGIN
                        ALTER TABLE Orders ADD DeliveryCountry NVARCHAR(100) NULL;
                    END
                ");
                logger.LogInformation("Delivery address columns ensured");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not ensure delivery address columns (may already exist)");
            }

            logger.LogInformation("Attempting database migration...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migration successful");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database not ready. Retries left: {Retries}", retries);
            retries--;
            await Task.Delay(5000);
        }
    }
    
    logger.LogInformation("OrderService started successfully");
}

await app.RunAsync();
