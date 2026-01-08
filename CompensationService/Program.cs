using CompensationService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
var rabbitMQHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitMQPort = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rabbitMQUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitMQPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is running"), tags: new[] { "self" })
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{rabbitMQUsername}:{rabbitMQPassword}@{rabbitMQHost}:{rabbitMQPort}/",
        name: "rabbitmq",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "rabbitmq" });

// Messaging
builder.Services.AddSingleton<IMessageProducer, RabbitMQProducer>();
builder.Services.AddSingleton<CompensationService.Application.Services.CompensationOrchestrator>();
builder.Services.AddHostedService<RabbitMQConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

