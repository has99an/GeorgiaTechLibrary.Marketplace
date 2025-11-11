using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NotificationService.Models;

namespace NotificationService.Services;

public class RabbitMQConsumer : BackgroundService, IMessageConsumer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly NotificationService _notificationService;
    private readonly ILogger<RabbitMQConsumer> _logger;

    // In-memory storage for orders (since no database)
    private readonly ConcurrentDictionary<Guid, OrderCreatedEvent> _orders = new();

    public RabbitMQConsumer(
        IConfiguration configuration,
        NotificationService notificationService,
        ILogger<RabbitMQConsumer> logger)
    {
        _notificationService = notificationService;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
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

            // Bind to routing keys for order events
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "OrderCreated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "OrderPaid");

            _logger.LogInformation("RabbitMQ Consumer connected and bound to order events");
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
        switch (routingKey)
        {
            case "OrderCreated":
                var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);
                if (orderCreatedEvent != null)
                {
                    await HandleOrderCreatedAsync(orderCreatedEvent);
                }
                break;

            case "OrderPaid":
                var orderPaidEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message);
                if (orderPaidEvent != null)
                {
                    await HandleOrderPaidAsync(orderPaidEvent);
                }
                break;
        }
    }

    private Task HandleOrderCreatedAsync(OrderCreatedEvent orderEvent)
    {
        // Store order in memory for later notification when paid
        _orders[orderEvent.OrderId] = orderEvent;
        _logger.LogInformation("Stored order {OrderId} with {ItemCount} items in memory",
            orderEvent.OrderId, orderEvent.OrderItems.Count);

        return Task.CompletedTask;
    }

    private Task HandleOrderPaidAsync(OrderPaidEvent orderPaidEvent)
    {
        // Retrieve the order from memory
        if (_orders.TryGetValue(orderPaidEvent.OrderId, out var order))
        {
            _logger.LogInformation("Processing notifications for paid order {OrderId}", orderPaidEvent.OrderId);

            // Group items by seller to avoid duplicate notifications
            var sellerNotifications = order.OrderItems
                .GroupBy(item => item.SellerId)
                .Select(group => new
                {
                    SellerId = group.Key,
                    Items = group.ToList()
                });

            // Send notification to each seller
            foreach (var sellerGroup in sellerNotifications)
            {
                foreach (var item in sellerGroup.Items)
                {
                    _notificationService.SendNotificationToSeller(
                        sellerGroup.SellerId,
                        order.OrderId,
                        item.BookISBN,
                        item.Quantity);
                }
            }

            // Clean up the order from memory after processing
            _orders.TryRemove(order.OrderId, out _);
            _logger.LogInformation("Removed processed order {OrderId} from memory", orderPaidEvent.OrderId);
        }
        else
        {
            _logger.LogWarning("Received OrderPaid event for unknown order {OrderId}", orderPaidEvent.OrderId);
        }

        return Task.CompletedTask;
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
