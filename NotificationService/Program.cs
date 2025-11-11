using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddHostedService<RabbitMQConsumer>();
builder.Services.AddSingleton<NotificationService.Services.NotificationService>();

var host = builder.Build();
host.Run();
