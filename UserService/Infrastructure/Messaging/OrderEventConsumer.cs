using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UserService.Application.Interfaces;
using UserService.Application.Services;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ consumer for handling OrderCreated events from OrderService
/// </summary>
public class OrderEventConsumer : BackgroundService
{
    private readonly ILogger<OrderEventConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderEventConsumer(
        ILogger<OrderEventConsumer> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for RabbitMQ to be ready
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        // Retry logic for RabbitMQ connection
        const int maxRetries = 15;
        const int initialDelaySeconds = 5;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ for Order events (attempt {Attempt}/{MaxRetries})...", 
                    attempt, maxRetries);
                
                InitializeRabbitMQ();
                
                if (_channel != null)
                {
                    var consumer = new EventingBasicConsumer(_channel);
                    consumer.Received += async (model, ea) =>
                    {
                        await HandleMessageAsync(ea, stoppingToken);
                    };

                    _channel.BasicConsume(
                        queue: "user_service_order_queue",
                        autoAck: false,
                        consumer: consumer);

                    _logger.LogInformation("Order event consumer started. Listening for OrderCreated, OrderDelivered, and OrderPaid events...");
                    
                    // Success - keep the service running
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries})", 
                    attempt, maxRetries);
                
                if (attempt < maxRetries)
                {
                    var delaySeconds = Math.Min(initialDelaySeconds * (int)Math.Pow(2, attempt - 1), 60);
                    _logger.LogInformation("Retrying in {Delay} seconds...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Failed to connect to RabbitMQ after {MaxRetries} attempts. Consumer will not start.", 
                        maxRetries);
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
            }
        }
    }

    private void InitializeRabbitMQ()
    {
        try
        {
            var hostName = _configuration["RabbitMQ:Host"] ?? "localhost";
            var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672");
            var userName = _configuration["RabbitMQ:Username"] ?? "guest";
            var password = _configuration["RabbitMQ:Password"] ?? "guest";
            
            _logger.LogInformation("Connecting to RabbitMQ at {HostName}:{Port} with user {UserName}", 
                hostName, port, userName);
            
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                SocketReadTimeout = TimeSpan.FromSeconds(30),
                SocketWriteTimeout = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchanges
            _channel.ExchangeDeclare(
                exchange: "order_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);
            
            // Declare book_events exchange for OrderPaid events (OrderService publishes OrderPaid to book_events)
            _channel.ExchangeDeclare(
                exchange: "book_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare queue
            _channel.QueueDeclare(
                queue: "user_service_order_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind queue to order_events exchange for OrderCreated and OrderDelivered events
            _channel.QueueBind(
                queue: "user_service_order_queue",
                exchange: "order_events",
                routingKey: "OrderCreated");
            
            _channel.QueueBind(
                queue: "user_service_order_queue",
                exchange: "order_events",
                routingKey: "OrderDelivered");
            
            // Bind queue to book_events exchange for OrderPaid events
            _channel.QueueBind(
                queue: "user_service_order_queue",
                exchange: "book_events",
                routingKey: "OrderPaid");

            _logger.LogInformation("Order event consumer initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Order event consumer");
            throw;
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("Received order event message with routing key: {RoutingKey}, Message: {Message}", routingKey, message);

            if (routingKey == "OrderCreated")
            {
                await HandleOrderCreatedAsync(message, cancellationToken);
            }
            else if (routingKey == "OrderDelivered")
            {
                await HandleOrderDeliveredAsync(message, cancellationToken);
            }
            else if (routingKey == "OrderPaid")
            {
                await HandleOrderPaidAsync(message, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
            }

            // Acknowledge the message
            _channel?.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OrderCreated message. RoutingKey: {RoutingKey}, Error: {Error}", 
                ea.RoutingKey, ex.Message);
            
            // Reject and requeue the message
            _channel?.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task HandleOrderCreatedAsync(string message, CancellationToken cancellationToken)
    {
        OrderCreatedEvent? orderEvent = null;
        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (orderEvent == null)
            {
                _logger.LogError("Failed to deserialize OrderCreated event. Message: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing OrderCreated event - OrderId: {OrderId}, PaymentStatus: {PaymentStatus}", 
                orderEvent.OrderId, orderEvent.PaymentStatus);

            // Only process paid orders
            if (orderEvent.PaymentStatus != "Paid")
            {
                _logger.LogInformation("Order {OrderId} is not paid, skipping seller stats update", orderEvent.OrderId);
                return;
            }

            // Create service scope
            using var scope = _serviceProvider.CreateScope();
            var sellerService = scope.ServiceProvider.GetRequiredService<ISellerService>();

            // Group order items by seller
            var sellerGroups = orderEvent.OrderItems?.GroupBy(item => item.SellerId) ?? Enumerable.Empty<IGrouping<string, OrderItem>>();

            foreach (var sellerGroup in sellerGroups)
            {
                if (!Guid.TryParse(sellerGroup.Key, out var sellerId))
                {
                    _logger.LogWarning("Invalid SellerId in order: {SellerId}", sellerGroup.Key);
                    continue;
                }

                var totalBooksSold = sellerGroup.Sum(item => item.Quantity);

                _logger.LogInformation("Updating seller stats - SellerId: {SellerId}, BooksSold: {BooksSold}", 
                    sellerId, totalBooksSold);

                // Update seller statistics (no rating for now, can be added later from feedback)
                await sellerService.UpdateSellerStatsFromOrderAsync(sellerId, totalBooksSold, null, cancellationToken);
            }

            _logger.LogInformation("OrderCreated event processed successfully - OrderId: {OrderId}", orderEvent.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event - OrderId: {OrderId}", 
                orderEvent?.OrderId ?? Guid.Empty);
            throw;
        }
    }

    private async Task HandleOrderDeliveredAsync(string message, CancellationToken cancellationToken)
    {
        OrderDeliveredEvent? orderEvent = null;
        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderDeliveredEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (orderEvent == null)
            {
                _logger.LogError("Failed to deserialize OrderDelivered event. Message: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing OrderDelivered event - OrderId: {OrderId}, CustomerId: {CustomerId}", 
                orderEvent.OrderId, orderEvent.CustomerId);

            // Order is delivered - customer can now leave reviews
            // We don't update stats here, just log that order is ready for review
            _logger.LogInformation("Order {OrderId} delivered to customer {CustomerId}. Customer can now leave reviews.", 
                orderEvent.OrderId, orderEvent.CustomerId);

            _logger.LogInformation("OrderDelivered event processed successfully - OrderId: {OrderId}", orderEvent.OrderId);
            
            await Task.CompletedTask; // Explicitly await to avoid warning
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderDelivered event - OrderId: {OrderId}", 
                orderEvent?.OrderId ?? Guid.Empty);
            throw;
        }
    }

    private async Task HandleOrderPaidAsync(string message, CancellationToken cancellationToken)
    {
        OrderPaidEvent? orderEvent = null;
        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (orderEvent == null)
            {
                _logger.LogError("Failed to deserialize OrderPaid event. Message: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing OrderPaid event - OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}, OrderItems: {ItemCount}",
                orderEvent.OrderId, orderEvent.CustomerId, orderEvent.TotalAmount, orderEvent.OrderItems?.Count ?? 0);

            // Create service scope
            using var scope = _serviceProvider.CreateScope();
            var sellerService = scope.ServiceProvider.GetRequiredService<ISellerService>();

            // Process each order item to update listing quantities
            if (orderEvent.OrderItems != null && orderEvent.OrderItems.Any())
            {
                int processedItems = 0;
                int failedItems = 0;

                foreach (var orderItem in orderEvent.OrderItems)
                {
                    try
                    {
                        _logger.LogInformation("Processing order item - OrderItemId: {OrderItemId}, BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}",
                            orderItem.OrderItemId, orderItem.BookISBN, orderItem.SellerId, orderItem.Quantity);

                        // Parse SellerId (can be Guid or string)
                        if (!Guid.TryParse(orderItem.SellerId, out var sellerId))
                        {
                            _logger.LogWarning("Invalid SellerId format in order item: {SellerId}, OrderItemId: {OrderItemId}",
                                orderItem.SellerId, orderItem.OrderItemId);
                            failedItems++;
                            continue;
                        }

                        // Update listing quantity (condition is not available in OrderPaid event, so pass null)
                        await sellerService.UpdateListingQuantityFromOrderAsync(
                            sellerId,
                            orderItem.BookISBN,
                            condition: null, // Condition not available in OrderPaid event
                            orderItem.Quantity,
                            cancellationToken);

                        processedItems++;
                        _logger.LogInformation("Order item processed successfully - OrderItemId: {OrderItemId}", orderItem.OrderItemId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order item - OrderItemId: {OrderItemId}, BookISBN: {BookISBN}, SellerId: {SellerId}",
                            orderItem.OrderItemId, orderItem.BookISBN, orderItem.SellerId);
                        failedItems++;
                        // Continue processing other items even if one fails
                    }
                }

                _logger.LogInformation("OrderPaid event processed - OrderId: {OrderId}, ProcessedItems: {ProcessedItems}, FailedItems: {FailedItems}",
                    orderEvent.OrderId, processedItems, failedItems);
            }
            else
            {
                _logger.LogWarning("OrderPaid event has no order items - OrderId: {OrderId}", orderEvent.OrderId);
            }

            _logger.LogInformation("OrderPaid event processed successfully - OrderId: {OrderId}", orderEvent.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderPaid event - OrderId: {OrderId}",
                orderEvent?.OrderId ?? Guid.Empty);
            throw;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        base.Dispose();
    }

    // Event model for deserialization
    private class OrderCreatedEvent
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? PaidDate { get; set; }
        public List<OrderItem>? OrderItems { get; set; }
    }

    private class OrderItem
    {
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    // Event model for OrderDelivered
    private class OrderDeliveredEvent
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime? DeliveredDate { get; set; }
    }

    // Event model for OrderPaid
    private class OrderPaidEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime? PaidDate { get; set; }
        public List<OrderPaidItem>? OrderItems { get; set; }
    }

    private class OrderPaidItem
    {
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}


