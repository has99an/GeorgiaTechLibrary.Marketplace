using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.ValueObjects;
using NotificationService.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Messaging;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly string _queueName;

    public RabbitMQConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQConsumer> logger)
    {
        _serviceProvider = serviceProvider;
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
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct, durable: true);

            // Declare DLQ exchange
            _channel.ExchangeDeclare(
                exchange: "book_events.dlq",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare DLQ queue
            var dlqQueueName = _channel.QueueDeclare(
                queue: "notificationservice_queue.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false).QueueName;
            _channel.QueueBind(queue: dlqQueueName, exchange: "book_events.dlq", routingKey: "failed");

            // Declare queue with DLQ configuration
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "book_events.dlq" },
                { "x-dead-letter-routing-key", "failed" }
            };
            _queueName = _channel.QueueDeclare(
                queue: "notificationservice_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs).QueueName;

            // Bind to routing keys - NotificationService listens to order events
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderCreated");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderPaid");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderShipped");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderDelivered");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderCancelled");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderRefunded");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "UserCreated");

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
                // Don't requeue - send to DLQ after max retries
                // Individual notification failures are handled internally
                _channel.BasicNack(ea.DeliveryTag, false, false); // Don't requeue - goes to DLQ
                _logger.LogWarning("Message NACKed and sent to DLQ (Delivery Tag: {DeliveryTag})", ea.DeliveryTag);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        switch (routingKey)
        {
            case "OrderCreated":
                await HandleOrderCreatedAsync(notificationService, message);
                break;
            case "OrderPaid":
                await HandleOrderPaidAsync(notificationService, message);
                break;
            case "OrderShipped":
                await HandleOrderShippedAsync(notificationService, message);
                break;
            case "OrderDelivered":
                await HandleOrderDeliveredAsync(notificationService, message);
                break;
            case "OrderCancelled":
                await HandleOrderCancelledAsync(notificationService, message);
                break;
            case "OrderRefunded":
                await HandleOrderRefundedAsync(notificationService, message);
                break;
            case "UserCreated":
                await HandleUserCreatedAsync(notificationService, message);
                break;
        }
    }

    private async Task HandleOrderCreatedAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderCreated event for order {OrderId}", orderEvent.OrderId);

        // Notify each seller about the order
        var sellerGroups = orderEvent.OrderItems.GroupBy(item => item.SellerId);
        
        foreach (var sellerGroup in sellerGroups)
        {
            var sellerId = sellerGroup.Key;
            var items = sellerGroup.ToList();
            
            try
            {
                await RetryPolicy.ExecuteWithRetryAsync(async () =>
                {
                    var itemsText = string.Join(", ", items.Select(i => $"{i.Quantity}x {i.BookISBN}"));
                    
                    var createDto = new CreateNotificationDto
                    {
                        RecipientId = sellerId,
                        RecipientEmail = $"{sellerId}@example.com", // TODO: Get actual seller email
                        Type = NotificationType.OrderCreated.ToString(),
                        Subject = $"New Order Received - Order #{orderEvent.OrderId}",
                        Message = $"You have received a new order!\n\nOrder ID: {orderEvent.OrderId}\nItems: {itemsText}\nTotal: ${orderEvent.TotalAmount:F2}\n\nPlease prepare the items for shipment.",
                        Metadata = new Dictionary<string, string>
                        {
                            ["OrderId"] = orderEvent.OrderId.ToString(),
                            ["CustomerId"] = orderEvent.CustomerId,
                            ["TotalAmount"] = orderEvent.TotalAmount.ToString("F2")
                        }
                    };

                    var notification = await notificationService.CreateNotificationAsync(createDto);
                    await notificationService.SendNotificationAsync(notification.NotificationId);
                    return true;
                }, maxRetries: 3, logger: _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to seller {SellerId} after all retries for OrderId: {OrderId}",
                    sellerId, orderEvent.OrderId);
                
                // Publish failure event (notification failures are less critical, but we still track them)
                // Note: NotificationService doesn't have IMessageProducer, so we'll log the failure
                // In a full implementation, we'd inject IMessageProducer here
                _logger.LogWarning("Notification failure logged - OrderId: {OrderId}, SellerId: {SellerId}, Error: {Error}",
                    orderEvent.OrderId, sellerId, ex.Message);
            }
        }
    }

    private async Task HandleOrderPaidAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderPaid event for order {OrderId}", orderEvent.OrderId);

        // Notify customer about successful payment
        var createDto = new CreateNotificationDto
        {
            RecipientId = orderEvent.CustomerId,
            RecipientEmail = $"{orderEvent.CustomerId}@example.com", // TODO: Get actual customer email
            Type = NotificationType.OrderPaid.ToString(),
            Subject = $"Payment Confirmed - Order #{orderEvent.OrderId}",
            Message = $"Your payment has been successfully processed!\n\nOrder ID: {orderEvent.OrderId}\nAmount Paid: ${orderEvent.TotalAmount:F2}\nPaid Date: {orderEvent.PaidDate:yyyy-MM-dd HH:mm}\n\nYour order will be shipped soon.",
            Metadata = new Dictionary<string, string>
            {
                ["OrderId"] = orderEvent.OrderId.ToString(),
                ["TotalAmount"] = orderEvent.TotalAmount.ToString("F2")
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    private async Task HandleOrderShippedAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderShippedEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderShipped event for order {OrderId}", orderEvent.OrderId);

        var createDto = new CreateNotificationDto
        {
            RecipientId = orderEvent.CustomerId,
            RecipientEmail = $"{orderEvent.CustomerId}@example.com",
            Type = NotificationType.OrderShipped.ToString(),
            Subject = $"Order Shipped - Order #{orderEvent.OrderId}",
            Message = $"Great news! Your order has been shipped.\n\nOrder ID: {orderEvent.OrderId}\nShipped Date: {orderEvent.ShippedDate:yyyy-MM-dd HH:mm}\n\nYou should receive it soon!",
            Metadata = new Dictionary<string, string>
            {
                ["OrderId"] = orderEvent.OrderId.ToString()
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    private async Task HandleOrderDeliveredAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderDeliveredEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderDelivered event for order {OrderId}", orderEvent.OrderId);

        var createDto = new CreateNotificationDto
        {
            RecipientId = orderEvent.CustomerId,
            RecipientEmail = $"{orderEvent.CustomerId}@example.com",
            Type = NotificationType.OrderDelivered.ToString(),
            Subject = $"Order Delivered - Order #{orderEvent.OrderId}",
            Message = $"Your order has been delivered!\n\nOrder ID: {orderEvent.OrderId}\nDelivered Date: {orderEvent.DeliveredDate:yyyy-MM-dd HH:mm}\n\nThank you for shopping with us!",
            Metadata = new Dictionary<string, string>
            {
                ["OrderId"] = orderEvent.OrderId.ToString()
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    private async Task HandleOrderCancelledAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderCancelledEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderCancelled event for order {OrderId}", orderEvent.OrderId);

        var createDto = new CreateNotificationDto
        {
            RecipientId = orderEvent.CustomerId,
            RecipientEmail = $"{orderEvent.CustomerId}@example.com",
            Type = NotificationType.OrderCancelled.ToString(),
            Subject = $"Order Cancelled - Order #{orderEvent.OrderId}",
            Message = $"Your order has been cancelled.\n\nOrder ID: {orderEvent.OrderId}\nReason: {orderEvent.Reason}\nCancelled Date: {orderEvent.CancelledDate:yyyy-MM-dd HH:mm}",
            Metadata = new Dictionary<string, string>
            {
                ["OrderId"] = orderEvent.OrderId.ToString(),
                ["Reason"] = orderEvent.Reason ?? "Not specified"
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    private async Task HandleOrderRefundedAsync(INotificationService notificationService, string message)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderRefundedEvent>(message);
        if (orderEvent == null) return;

        _logger.LogInformation("Processing OrderRefunded event for order {OrderId}", orderEvent.OrderId);

        var createDto = new CreateNotificationDto
        {
            RecipientId = orderEvent.CustomerId,
            RecipientEmail = $"{orderEvent.CustomerId}@example.com",
            Type = NotificationType.OrderRefunded.ToString(),
            Subject = $"Refund Processed - Order #{orderEvent.OrderId}",
            Message = $"Your refund has been processed.\n\nOrder ID: {orderEvent.OrderId}\nRefund Amount: ${orderEvent.TotalAmount:F2}\nReason: {orderEvent.Reason}\nRefunded Date: {orderEvent.RefundedDate:yyyy-MM-dd HH:mm}\n\nPlease allow 5-7 business days for the refund to appear in your account.",
            Metadata = new Dictionary<string, string>
            {
                ["OrderId"] = orderEvent.OrderId.ToString(),
                ["TotalAmount"] = orderEvent.TotalAmount.ToString("F2"),
                ["Reason"] = orderEvent.Reason ?? "Not specified"
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    private async Task HandleUserCreatedAsync(INotificationService notificationService, string message)
    {
        var userEvent = JsonSerializer.Deserialize<UserCreatedEvent>(message);
        if (userEvent == null) return;

        _logger.LogInformation("Processing UserCreated event for user {UserId}", userEvent.UserId);

        var createDto = new CreateNotificationDto
        {
            RecipientId = userEvent.UserId,
            RecipientEmail = userEvent.Email,
            Type = NotificationType.System.ToString(),
            Subject = "Welcome to Georgia Tech Marketplace!",
            Message = $"Welcome {userEvent.Name}!\n\nThank you for joining Georgia Tech Marketplace. We're excited to have you as part of our community.\n\nYou can now browse books, place orders, and manage your account.",
            Metadata = new Dictionary<string, string>
            {
                ["UserId"] = userEvent.UserId,
                ["Name"] = userEvent.Name
            }
        };

        var notification = await notificationService.CreateNotificationAsync(createDto);
        await notificationService.SendNotificationAsync(notification.NotificationId);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    // Event models
    private class OrderCreatedEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemEvent> OrderItems { get; set; } = new();
    }

    private class OrderItemEvent
    {
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private class OrderPaidEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime PaidDate { get; set; }
    }

    private class OrderShippedEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public DateTime ShippedDate { get; set; }
    }

    private class OrderDeliveredEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public DateTime DeliveredDate { get; set; }
    }

    private class OrderCancelledEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public DateTime CancelledDate { get; set; }
        public string? Reason { get; set; }
    }

    private class OrderRefundedEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime RefundedDate { get; set; }
        public string? Reason { get; set; }
    }

    private class UserCreatedEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

