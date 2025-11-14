using Yarp.ReverseProxy.Transforms;
using HealthChecks.Uris;
using ApiGateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:books-cluster:Destinations:books-destination:Address"] + "/health"), name: "BookService")
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:warehouse-cluster:Destinations:warehouse-destination:Address"] + "/health"), name: "WarehouseService")
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:search-cluster:Destinations:search-destination:Address"] + "/health"), name: "SearchService")
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:orders-cluster:Destinations:orders-destination:Address"] + "/health"), name: "OrderService")
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:auth-cluster:Destinations:auth-destination:Address"] + "/health"), name: "AuthService")
    .AddUrlGroup(new Uri(builder.Configuration["ReverseProxy:Clusters:users-cluster:Destinations:users-destination:Address"] + "/health"), name: "UserService");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        // Add Swagger endpoints for each service
        options.SwaggerEndpoint("/swagger/auth/swagger.json", "Auth Service API");
        options.SwaggerEndpoint("/swagger/books/swagger.json", "Book Service API");
        options.SwaggerEndpoint("/swagger/warehouse/swagger.json", "Warehouse Service API");
        options.SwaggerEndpoint("/swagger/search/swagger.json", "Search Service API");
        options.SwaggerEndpoint("/swagger/orders/swagger.json", "Order Service API");
        options.SwaggerEndpoint("/swagger/users/swagger.json", "User Service API");
    });
}

app.UseHttpsRedirection();

// Add JWT authentication middleware
app.UseMiddleware<JwtAuthenticationMiddleware>();

// Health check endpoint
app.MapHealthChecks("/health");

// Swagger aggregation endpoints
app.MapGet("/swagger/auth/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:auth-cluster:Destinations:auth-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/books/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:books-cluster:Destinations:books-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/warehouse/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:warehouse-cluster:Destinations:warehouse-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/search/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:search-cluster:Destinations:search-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/orders/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:orders-cluster:Destinations:orders-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/users/swagger.json", async (IConfiguration config) =>
{
    using var client = new HttpClient();
    try
    {
        var baseUrl = config["ReverseProxy:Clusters:users-cluster:Destinations:users-destination:Address"];
        var response = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

// Enable reverse proxy
app.MapReverseProxy();

app.Run();
