using SearchService.Repositories;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add health checks
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Add repositories
builder.Services.AddScoped<ISearchRepository, SearchRepository>();

// Add message consumer as hosted service
builder.Services.AddHostedService<RabbitMQConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
