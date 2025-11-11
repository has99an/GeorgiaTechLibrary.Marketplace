using Yarp.ReverseProxy.Transforms;
using HealthChecks.Uris;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://localhost:5000/health"), name: "BookService")
    .AddUrlGroup(new Uri("http://localhost:5001/health"), name: "WarehouseService")
    .AddUrlGroup(new Uri("http://localhost:5002/health"), name: "SearchService")
    .AddUrlGroup(new Uri("http://localhost:5003/health"), name: "OrderService");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        // Add Swagger endpoints for each service
        options.SwaggerEndpoint("/swagger/books/swagger.json", "Book Service API");
        options.SwaggerEndpoint("/swagger/warehouse/swagger.json", "Warehouse Service API");
        options.SwaggerEndpoint("/swagger/search/swagger.json", "Search Service API");
        options.SwaggerEndpoint("/swagger/orders/swagger.json", "Order Service API");
    });
}

app.UseHttpsRedirection();

// Health check endpoint
app.MapHealthChecks("/health");

// Swagger aggregation endpoints
app.MapGet("/swagger/books/swagger.json", async () =>
{
    using var client = new HttpClient();
    try
    {
        var response = await client.GetStringAsync("http://localhost:5000/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/warehouse/swagger.json", async () =>
{
    using var client = new HttpClient();
    try
    {
        var response = await client.GetStringAsync("http://localhost:5001/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/search/swagger.json", async () =>
{
    using var client = new HttpClient();
    try
    {
        var response = await client.GetStringAsync("http://localhost:5002/swagger/v1/swagger.json");
        return Results.Content(response, "application/json");
    }
    catch
    {
        return Results.NotFound();
    }
});

app.MapGet("/swagger/orders/swagger.json", async () =>
{
    using var client = new HttpClient();
    try
    {
        var response = await client.GetStringAsync("http://localhost:5003/swagger/v1/swagger.json");
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
