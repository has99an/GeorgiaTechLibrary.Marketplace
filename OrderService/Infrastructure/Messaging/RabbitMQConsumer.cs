using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Infrastructure.Messaging;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly string _queueName;

    public RabbitMQConsumer(IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
    {
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
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct, durable: true);

            // Declare queue
            _queueName = _channel.QueueDeclare(
                queue: "orderservice_queue",
                durable: true,
                exclusive: false,
                autoDelete: false).QueueName;

            // Bind to routing keys - OrderService listens to BookStockUpdated
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "BookStockUpdated");

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
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with routing key {RoutingKey}", routingKey);
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private Task ProcessMessageAsync(string routingKey, string message)
    {
        switch (routingKey)
        {
            case "BookStockUpdated":
                var stockEvent = JsonSerializer.Deserialize<BookStockUpdatedEvent>(message);
                if (stockEvent != null)
                {
                    HandleStockUpdateAsync(stockEvent);
                }
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleStockUpdateAsync(BookStockUpdatedEvent stockEvent)
    {
        _logger.LogInformation("Received stock update for book ISBN {ISBN}, seller {SellerId}, quantity {Quantity}",
            stockEvent.BookISBN, stockEvent.SellerId, stockEvent.Quantity);

        // TODO: Update local cache or validate pending orders against new stock levels
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private class BookStockUpdatedEvent
    {
        public int Id { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Condition { get; set; } = string.Empty;
    }
}

