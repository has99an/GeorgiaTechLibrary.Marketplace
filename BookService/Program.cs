using BookService.Data;
using BookService.Repositories;
using BookService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
var rabbitMQHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitMQPort = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rabbitMQUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitMQPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Service is running"), tags: new[] { "self" })
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "database" })
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{rabbitMQUsername}:{rabbitMQPassword}@{rabbitMQHost}:{rabbitMQPort}/",
        name: "rabbitmq",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "rabbitmq" });

// Add Entity Framework
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add repositories
builder.Services.AddScoped<IBookRepository, BookRepository>();

// Add message producer
builder.Services.AddSingleton<IMessageProducer, RabbitMQProducer>();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Add logging
builder.Services.AddLogging();

await using var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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

// Wait for SQL Server to be ready and seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var retries = 10;

    while (retries > 0)
    {
        try
        {
            Console.WriteLine("Attempting database migration...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("Migration successful. Starting seed data...");
            await SeedData.Initialize(dbContext);
            Console.WriteLine("Seed data completed successfully");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database not ready: {ex.Message}. Retries left: {retries}");
            retries--;
            await Task.Delay(5000);
        }
    }
}

await app.RunAsync();
