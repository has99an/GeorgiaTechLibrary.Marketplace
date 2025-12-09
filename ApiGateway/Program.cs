using ApiGateway.Extensions;
using ApiGateway.Middleware;
using ApiGateway.Policies;
using ApiGateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Add API Gateway services (CORS, Caching, HTTP Clients, etc.)
builder.Services.AddApiGatewayServices(builder.Configuration);

// Add YARP Reverse Proxy with configuration
builder.Services.AddYarpConfiguration(builder.Configuration);

// Add Health Checks for all downstream services
builder.Services.AddApiGatewayHealthChecks(builder.Configuration);

// Add HTTP client for token validation
builder.Services.AddHttpClient<ITokenValidationService, TokenValidationService>();

var app = builder.Build();

// Configure the HTTP request pipeline

// 1. Exception handling (must be first)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Request logging
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// 4. CORS (must be before authentication)
app.UseCors("ApiGatewayPolicy");

// 5. Response compression
app.UseResponseCompression();

// 6. HTTPS redirection
app.UseHttpsRedirection();

// 7. Rate limiting
app.UseMiddleware<RateLimitingMiddleware>();

// 8. JWT authentication
app.UseMiddleware<JwtAuthenticationMiddleware>();

// Swagger UI (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/auth/swagger.json", "Auth Service API");
        options.SwaggerEndpoint("/swagger/books/swagger.json", "Book Service API");
        options.SwaggerEndpoint("/swagger/warehouse/swagger.json", "Warehouse Service API");
        options.SwaggerEndpoint("/swagger/search/swagger.json", "Search Service API");
        options.SwaggerEndpoint("/swagger/orders/swagger.json", "Order Service API");
        options.SwaggerEndpoint("/swagger/users/swagger.json", "User Service API");
        options.RoutePrefix = "swagger";
    });
}

// Health check endpoint
app.MapHealthChecks("/health");

// Swagger aggregation endpoints
app.MapGet("/swagger/{service}/swagger.json", async (
    string service,
    ISwaggerAggregationService swaggerService) =>
{
    var document = await swaggerService.GetSwaggerDocumentAsync(service);
    return document != null
        ? Results.Content(document, "application/json")
        : Results.NotFound(new { error = $"Swagger document not found for service: {service}" });
})
.WithName("GetSwaggerDocument")
.WithTags("Swagger");

// API Gateway info endpoint
app.MapGet("/", () => new
{
    name = "Georgia Tech Library Marketplace - API Gateway",
    version = "2.0",
    status = "running",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        health = "/health",
        swagger = "/swagger",
        services = new[]
        {
            new { name = "AuthService", path = "/auth/*" },
            new { name = "BookService", path = "/books/*" },
            new { name = "WarehouseService", path = "/warehouse/*" },
            new { name = "SearchService", path = "/search/*" },
            new { name = "OrderService", path = "/orders/*" },
            new { name = "ShoppingCartService", path = "/cart/*" },
            new { name = "UserService", path = "/users/*" },
            new { name = "UserService (Sellers)", path = "/sellers/*" },
            new { name = "NotificationService", path = "/notifications/*" }
        }
    }
})
.WithName("GetGatewayInfo")
.WithTags("Gateway")
.ExcludeFromDescription();

// Enable reverse proxy (must be last)
app.MapReverseProxy();

app.Run();
