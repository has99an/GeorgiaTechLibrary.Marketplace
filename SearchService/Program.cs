using SearchService.API.Extensions;
using SearchService.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ===== Clean Architecture Layers =====

// Add Application Layer (Use Cases, Handlers, Behaviors)
builder.Services.AddApplicationServices();

// Add Infrastructure Layer (Repositories, External Services)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add API Layer (Controllers, Swagger, Health Checks)
builder.Services.AddApiServices(builder.Configuration);

// Add Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Add Logging
builder.Services.AddLogging();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("SearchServiceCorsPolicy", policy =>
    {
        // In production, specify exact origins
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000", "http://localhost:5173" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-RateLimit-Limit-Minute", "X-RateLimit-Remaining-Minute", 
                                 "X-RateLimit-Limit-Hour", "X-RateLimit-Remaining-Hour")
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

var app = builder.Build();

// ===== Middleware Pipeline =====
// Order is critical for security and performance!

// 1. Security headers (first in pipeline)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Request size limits (early protection)
app.UseMiddleware<RequestSizeLimitMiddleware>();

// 3. Response compression (should be early in pipeline)
app.UseResponseCompression();

// 4. CORS (before authentication/authorization)
app.UseCors("SearchServiceCorsPolicy");

// 5. Rate limiting (before business logic)
app.UseMiddleware<RateLimitingMiddleware>();

// 6. Global exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 7. Response sanitization (after processing, before sending)
app.UseMiddleware<ResponseSanitizationMiddleware>();

// Swagger (always enabled for development and documentation)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchService API v2.0 - Clean Architecture");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "SearchService API Documentation - Clean Architecture + CQRS";
});

// Don't use HTTPS redirection in Docker
// app.UseHttpsRedirection();

// Map endpoints
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
    Predicate = check => check.Tags.Contains("redis") || check.Tags.Contains("database"),
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

app.Run();
