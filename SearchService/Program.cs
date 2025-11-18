using SearchService.Repositories;
using SearchService.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SearchService API",
        Version = "v1",
        Description = "Fast search functionality for GeorgiaTechLibrary.Marketplace using Redis cache. Provides book search, availability filtering, and seller information aggregation.",
        Contact = new OpenApiContact
        {
            Name = "GeorgiaTechLibrary.Marketplace Team"
        }
    });

    // Add XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Add AutoMapper with explicit assembly scanning
builder.Services.AddAutoMapper(typeof(Program));

// Add repositories
builder.Services.AddScoped<ISearchRepository, SearchRepository>();

// Add index builder - runs at startup to build sorted sets
builder.Services.AddHostedService<IndexBuilderService>();

// Add message consumer as hosted service
builder.Services.AddHostedService<RabbitMQConsumer>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchService API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "SearchService API Documentation";
});

// Don't use HTTPS redirection in Docker
// app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
