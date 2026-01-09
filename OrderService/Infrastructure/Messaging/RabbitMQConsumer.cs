using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Interfaces;
using OrderService.Domain.ValueObjects;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Infrastructure.Messaging;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _queueName;

    public RabbitMQConsumer(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<RabbitMQConsumer> logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

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

            // Declare DLQ exchange
            _channel.ExchangeDeclare(
                exchange: "book_events.dlq",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare DLQ queue
            var dlqQueueName = _channel.QueueDeclare(
                queue: "orderservice_queue.dlq",
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
                queue: "orderservice_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs).QueueName;

            // Bind to routing keys - OrderService listens to BookStockUpdated and compensation events
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "BookStockUpdated");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "CompensationRequired");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "InventoryReservationFailed");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "SellerStatsUpdateFailed");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderCancellationRequested");

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
                _channel.BasicNack(ea.DeliveryTag, false, false);
                _logger.LogWarning("Message NACKed and sent to DLQ (Delivery Tag: {DeliveryTag})", ea.DeliveryTag);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
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
            case "CompensationRequired":
                var compensationEvent = JsonSerializer.Deserialize<CompensationRequiredEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (compensationEvent != null)
                {
                    await HandleCompensationRequiredAsync(compensationEvent);
                }
                break;
            case "InventoryReservationFailed":
                var inventoryFailedEvent = JsonSerializer.Deserialize<InventoryReservationFailedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (inventoryFailedEvent != null)
                {
                    await HandleInventoryReservationFailedAsync(inventoryFailedEvent);
                }
                break;
            case "SellerStatsUpdateFailed":
                var sellerStatsFailedEvent = JsonSerializer.Deserialize<SellerStatsUpdateFailedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (sellerStatsFailedEvent != null)
                {
                    await HandleSellerStatsUpdateFailedAsync(sellerStatsFailedEvent);
                }
                break;
            case "OrderCancellationRequested":
                var cancellationEvent = JsonSerializer.Deserialize<OrderCancellationRequestedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (cancellationEvent != null)
                {
                    await HandleOrderCancellationRequestedAsync(cancellationEvent);
                }
                break;
        }
    }

    private void HandleStockUpdateAsync(BookStockUpdatedEvent stockEvent)
    {
        _logger.LogInformation("Received stock update for book ISBN {ISBN}, seller {SellerId}, quantity {Quantity}",
            stockEvent.BookISBN, stockEvent.SellerId, stockEvent.Quantity);

        // TODO: Update local cache or validate pending orders against new stock levels
    }

    private async Task HandleCompensationRequiredAsync(CompensationRequiredEvent compensationEvent)
    {
        _logger.LogInformation("Processing CompensationRequired event - OrderId: {OrderId}, FailedItems: {Count}",
            compensationEvent.OrderId, compensationEvent.FailedItems?.Count ?? 0);

        using var scope = _serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();

        var order = await orderRepository.GetByIdAsync(compensationEvent.OrderId);
        if (order == null)
        {
            _logger.LogWarning("Order not found for compensation - OrderId: {OrderId}", compensationEvent.OrderId);
            return;
        }

        foreach (var failedItem in compensationEvent.FailedItems ?? new List<FailedItem>())
        {
            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.OrderItemId == failedItem.OrderItemId);
            if (orderItem == null)
            {
                _logger.LogWarning("OrderItem not found for compensation - OrderItemId: {OrderItemId}", failedItem.OrderItemId);
                continue;
            }

            var oldStatus = orderItem.Status;
            orderItem.MarkAsFailed();
            await orderRepository.UpdateAsync(order);

            // Publish status changed event
            var statusChangedEvent = new
            {
                OrderId = order.OrderId,
                OrderItemId = orderItem.OrderItemId,
                OldStatus = oldStatus.ToString(),
                NewStatus = orderItem.Status.ToString(),
                ChangedAt = DateTime.UtcNow,
                Reason = $"Compensation required: {failedItem.FailureType}"
            };
            messageProducer.SendMessage(statusChangedEvent, "OrderItemStatusChanged");

            // Trigger compensation based on failure type
            if (failedItem.FailureType == "InventoryReservation")
            {
                var compensateEvent = new
                {
                    OrderId = order.OrderId,
                    OrderItemId = orderItem.OrderItemId,
                    BookISBN = orderItem.BookISBN,
                    SellerId = orderItem.SellerId,
                    Quantity = orderItem.Quantity,
                    RequestedAt = DateTime.UtcNow
                };
                messageProducer.SendMessage(compensateEvent, "CompensateInventoryReservation");
            }
            else if (failedItem.FailureType == "SellerStatsUpdate")
            {
                var compensateEvent = new
                {
                    OrderId = order.OrderId,
                    OrderItemId = orderItem.OrderItemId,
                    SellerId = orderItem.SellerId,
                    BookISBN = orderItem.BookISBN,
                    Quantity = orderItem.Quantity,
                    RequestedAt = DateTime.UtcNow
                };
                messageProducer.SendMessage(compensateEvent, "CompensateSellerStatsUpdate");
            }
        }
    }

    private async Task HandleInventoryReservationFailedAsync(InventoryReservationFailedEvent failedEvent)
    {
        _logger.LogInformation("Processing InventoryReservationFailed event - OrderId: {OrderId}, OrderItemId: {OrderItemId}",
            failedEvent.OrderId, failedEvent.OrderItemId);

        using var scope = _serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();

        var order = await orderRepository.GetByIdAsync(failedEvent.OrderId);
        if (order == null)
        {
            _logger.LogWarning("Order not found - OrderId: {OrderId}", failedEvent.OrderId);
            return;
        }

        var orderItem = order.OrderItems.FirstOrDefault(oi => oi.OrderItemId == failedEvent.OrderItemId);
        if (orderItem == null)
        {
            _logger.LogWarning("OrderItem not found - OrderItemId: {OrderItemId}", failedEvent.OrderItemId);
            return;
        }

        var oldStatus = orderItem.Status;
        orderItem.MarkAsFailed();
        await orderRepository.UpdateAsync(order);

        // Publish status changed event
        var statusChangedEvent = new
        {
            OrderId = order.OrderId,
            OrderItemId = orderItem.OrderItemId,
            OldStatus = oldStatus.ToString(),
            NewStatus = orderItem.Status.ToString(),
            ChangedAt = DateTime.UtcNow,
            Reason = $"Inventory reservation failed: {failedEvent.ErrorMessage}"
        };
        messageProducer.SendMessage(statusChangedEvent, "OrderItemStatusChanged");
    }

    private async Task HandleSellerStatsUpdateFailedAsync(SellerStatsUpdateFailedEvent failedEvent)
    {
        _logger.LogInformation("Processing SellerStatsUpdateFailed event - OrderId: {OrderId}, OrderItemId: {OrderItemId}",
            failedEvent.OrderId, failedEvent.OrderItemId);

        using var scope = _serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();

        var order = await orderRepository.GetByIdAsync(failedEvent.OrderId);
        if (order == null)
        {
            _logger.LogWarning("Order not found - OrderId: {OrderId}", failedEvent.OrderId);
            return;
        }

        var orderItem = order.OrderItems.FirstOrDefault(oi => oi.OrderItemId == failedEvent.OrderItemId);
        if (orderItem == null)
        {
            _logger.LogWarning("OrderItem not found - OrderItemId: {OrderItemId}", failedEvent.OrderItemId);
            return;
        }

        var oldStatus = orderItem.Status;
        orderItem.MarkAsFailed();
        await orderRepository.UpdateAsync(order);

        // Publish status changed event
        var statusChangedEvent = new
        {
            OrderId = order.OrderId,
            OrderItemId = orderItem.OrderItemId,
            OldStatus = oldStatus.ToString(),
            NewStatus = orderItem.Status.ToString(),
            ChangedAt = DateTime.UtcNow,
            Reason = $"Seller stats update failed: {failedEvent.ErrorMessage}"
        };
        messageProducer.SendMessage(statusChangedEvent, "OrderItemStatusChanged");
    }

    private async Task HandleOrderCancellationRequestedAsync(OrderCancellationRequestedEvent cancellationEvent)
    {
        _logger.LogInformation("Processing OrderCancellationRequested event - OrderId: {OrderId}, Reason: {Reason}",
            cancellationEvent.OrderId, cancellationEvent.Reason);

        using var scope = _serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        var order = await orderRepository.GetByIdAsync(cancellationEvent.OrderId);
        if (order == null)
        {
            _logger.LogWarning("Order not found for cancellation - OrderId: {OrderId}", cancellationEvent.OrderId);
            return;
        }

        // Check if order is already cancelled
        if (order.Status == OrderStatus.Cancelled)
        {
            _logger.LogInformation("Order already cancelled - OrderId: {OrderId}", cancellationEvent.OrderId);
            return;
        }

        // Check if order can be cancelled (must be Paid status for compensation scenario)
        if (order.Status != OrderStatus.Paid && order.Status != OrderStatus.Pending)
        {
            _logger.LogWarning("Order cannot be cancelled - OrderId: {OrderId}, Status: {Status}", 
                cancellationEvent.OrderId, order.Status);
            return;
        }

        _logger.LogInformation("Cancelling order - OrderId: {OrderId}, Original Status: {Status}", 
            cancellationEvent.OrderId, order.Status);

        // Process refund if order was paid
        if (order.Status == OrderStatus.Paid && order.PaidDate.HasValue)
        {
            _logger.LogInformation("Processing refund for paid order - OrderId: {OrderId}, Amount: {Amount}",
                cancellationEvent.OrderId, order.TotalAmount.Amount);

            try
            {
                var refundResult = await paymentService.ProcessRefundAsync(
                    cancellationEvent.OrderId,
                    order.TotalAmount.Amount,
                    cancellationEvent.Reason);

                if (!refundResult.Success)
                {
                    _logger.LogError("Refund failed for order - OrderId: {OrderId}, Error: {Error}",
                        cancellationEvent.OrderId, refundResult.Message);
                    // Continue with cancellation even if refund fails - manual intervention may be needed
                }
                else
                {
                    _logger.LogInformation("Refund processed successfully - OrderId: {OrderId}", cancellationEvent.OrderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during refund processing - OrderId: {OrderId}", cancellationEvent.OrderId);
                // Continue with cancellation
            }
        }

        // Cancel the order
        order.Cancel(cancellationEvent.Reason);
        await orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order cancelled successfully - OrderId: {OrderId}", cancellationEvent.OrderId);

        // Mark all items as compensated
        foreach (var orderItem in order.OrderItems)
        {
            var oldStatus = orderItem.Status;
            if (oldStatus != OrderItemStatus.Failed && oldStatus != OrderItemStatus.Compensated)
            {
                orderItem.MarkAsFailed(); // Mark as failed if not already
            }
            
            // Publish status changed event for each item
            var statusChangedEvent = new
            {
                OrderId = order.OrderId,
                OrderItemId = orderItem.OrderItemId,
                OldStatus = oldStatus.ToString(),
                NewStatus = "Compensated", // Custom status for UI purposes
                ChangedAt = DateTime.UtcNow,
                Reason = "Order cancelled after compensation"
            };
            messageProducer.SendMessage(statusChangedEvent, "OrderItemStatusChanged");
        }

        // Publish OrderCancelled event
        var orderCancelledEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            CancelledDate = order.CancelledDate,
            Reason = order.CancellationReason,
            RefundProcessed = order.Status == OrderStatus.Paid,
            OrderItems = order.OrderItems.Select(item => new
            {
                OrderItemId = item.OrderItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity
            }).ToList()
        };
        messageProducer.SendMessage(orderCancelledEvent, "OrderCancelled");

        _logger.LogInformation("Published OrderCancelled event - OrderId: {OrderId}", cancellationEvent.OrderId);
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

    private class CompensationRequiredEvent
    {
        public Guid OrderId { get; set; }
        public List<FailedItem> FailedItems { get; set; } = new();
        public DateTime RequestedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private class FailedItem
    {
        public Guid OrderItemId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    private class InventoryReservationFailedEvent
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public int RetryAttempts { get; set; }
    }

    private class SellerStatsUpdateFailedEvent
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public string SellerId { get; set; } = string.Empty;
        public string BookISBN { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public int RetryAttempts { get; set; }
    }

    private class OrderCancellationRequestedEvent
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public List<FailedItem> FailedItems { get; set; } = new();
    }
}

