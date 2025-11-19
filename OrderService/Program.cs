using OrderService.API.Extensions;
using OrderService.API.Middleware;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
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
app.MapHealthChecks("/health");

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
}

logger.LogInformation("OrderService started successfully");

await app.RunAsync();
