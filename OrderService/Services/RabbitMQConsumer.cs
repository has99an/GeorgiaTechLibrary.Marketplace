using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OrderService.Application.Interfaces;

namespace OrderService.Services;

public class RabbitMQConsumer : BackgroundService, IMessageConsumer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumer> _logger;

    public RabbitMQConsumer(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<RabbitMQConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct);

            // Declare queue
            var queueName = _channel.QueueDeclare().QueueName;

            // Bind to routing keys - OrderService listens to BookStockUpdated
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookStockUpdated");

            _logger.LogInformation("RabbitMQ Consumer connected and bound to events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            try
            {
                await ProcessMessageAsync(routingKey, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with routing key {RoutingKey}", routingKey);
            }
        };

        _channel.BasicConsume(queue: "", autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        switch (routingKey)
        {
            case "BookStockUpdated":
                var stockEvent = JsonSerializer.Deserialize<OrderService.Models.WarehouseStockEvent>(message);
                if (stockEvent != null)
                {
                    await HandleStockUpdateAsync(orderRepository, stockEvent);
                }
                break;
        }
    }

    private async Task HandleStockUpdateAsync(IOrderRepository repository, OrderService.Models.WarehouseStockEvent stockEvent)
    {
        // This method can be used to validate stock availability when creating orders
        // For now, we'll just log the stock update
        _logger.LogInformation("Received stock update for book ISBN {Isbn}, seller {SellerId}, quantity {Quantity}",
            stockEvent.BookISBN, stockEvent.SellerId, stockEvent.Quantity);
    }

    public void StartConsuming()
    {
        // Consumer starts automatically via BackgroundService
    }

    public void StopConsuming()
    {
        _channel?.Close();
        _connection?.Close();
    }

    public override void Dispose()
    {
        StopConsuming();
        base.Dispose();
    }
}
