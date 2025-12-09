using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WarehouseService.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace WarehouseService.Services;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _queueName;

    public RabbitMQConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQConsumer> logger)
    {
        try
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            _logger.LogInformation("=== WAREHOUSE RABBITMQ CONSUMER INITIALIZATION STARTED ===");
            _logger.LogInformation("Step 1: Reading RabbitMQ configuration...");

            var hostName = configuration["RabbitMQ:Host"] ?? "localhost";
            var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
            var userName = configuration["RabbitMQ:Username"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";

            _logger.LogInformation("Step 1: Configuration read - Host: {Host}, Port: {Port}, Username: {Username}",
                hostName, port, userName);

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password
            };

            _logger.LogInformation("Step 2: RabbitMQ connection factory created");

            _logger.LogInformation("Step 3: Creating RabbitMQ connection...");
            _connection = factory.CreateConnection();
            _logger.LogInformation("Step 3: Connection created successfully");

            _logger.LogInformation("Step 4: Creating RabbitMQ channel...");
            _channel = _connection.CreateModel();
            _logger.LogInformation("Step 4: Channel created successfully");

            _logger.LogInformation("Step 5: Declaring exchange 'book_events'...");
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct, durable: true);
            _logger.LogInformation("Step 5: Exchange 'book_events' declared successfully");

            _logger.LogInformation("Step 6: Declaring queue 'warehouseservice_order_queue'...");
            _queueName = _channel.QueueDeclare(
                queue: "warehouseservice_order_queue",
                durable: true,
                exclusive: false,
                autoDelete: false).QueueName;
            _logger.LogInformation("Step 6: Queue '{QueueName}' declared successfully", _queueName);

            _logger.LogInformation("Step 7: Binding queue to exchange with routing key 'OrderPaid'...");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderPaid");
            _logger.LogInformation("Step 7: Queue bound successfully to 'book_events' exchange with routing key 'OrderPaid'");

            _logger.LogInformation("=== WAREHOUSE RABBITMQ CONSUMER INITIALIZED SUCCESSFULLY ===");
            _logger.LogInformation("Ready to receive OrderPaid events from RabbitMQ");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "=== FAILED TO INITIALIZE RABBITMQ CONSUMER ===");
            _logger?.LogError("Error: {Error}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            // Don't throw - let it fail gracefully so service can still start
            // The consumer will retry in ExecuteAsync
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== STARTING RABBITMQ CONSUMER EXECUTION ===");
        stoppingToken.ThrowIfCancellationRequested();

        if (_connection == null || !_connection.IsOpen)
        {
            _logger.LogError("=== RABBITMQ CONSUMER EXECUTION FAILED ===");
            _logger.LogError("Connection is null or not open. Consumer will not process messages.");
            return Task.CompletedTask;
        }

        if (_channel == null || _channel.IsClosed)
        {
            _logger.LogError("=== RABBITMQ CONSUMER EXECUTION FAILED ===");
            _logger.LogError("Channel is null or closed. Consumer will not process messages.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Connection and channel are valid. Setting up consumer...");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;
            var deliveryTag = ea.DeliveryTag;

            _logger.LogInformation("=== MESSAGE RECEIVED ===");
            _logger.LogInformation("Routing Key: {RoutingKey}, Delivery Tag: {DeliveryTag}, Message Length: {MessageLength}",
                routingKey, deliveryTag, message.Length);

            try
            {
                await ProcessMessageAsync(routingKey, message);
                _channel.BasicAck(deliveryTag, false);
                _logger.LogInformation("Message acknowledged successfully (Delivery Tag: {DeliveryTag})", deliveryTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR PROCESSING MESSAGE ===");
                _logger.LogError("Routing Key: {RoutingKey}, Delivery Tag: {DeliveryTag}, Error: {Error}",
                    routingKey, deliveryTag, ex.Message);
                _channel.BasicNack(deliveryTag, false, true); // Requeue on failure
                _logger.LogWarning("Message NACKed and requeued (Delivery Tag: {DeliveryTag})", deliveryTag);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("=== RABBITMQ CONSUMER STARTED ===");
        _logger.LogInformation("Listening for messages on queue: {QueueName}", _queueName);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
    {
        _logger.LogInformation("=== PROCESSING MESSAGE ===");
        _logger.LogInformation("Routing Key: {RoutingKey}", routingKey);

        if (routingKey == "OrderPaid")
        {
            _logger.LogInformation("Routing key matches 'OrderPaid' - processing order...");
            await HandleOrderPaidAsync(message);
        }
        else
        {
            _logger.LogWarning("Received message with unknown routing key: {RoutingKey}. Ignoring...", routingKey);
        }
    }

    private async Task HandleOrderPaidAsync(string message)
    {
        _logger.LogInformation("=== HANDLING ORDERPAID EVENT ===");
        _logger.LogInformation("Step 1: Deserializing OrderPaid event...");
        _logger.LogInformation("Raw message: {Message}", message);

        OrderPaidEvent? orderEvent = null;
        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (orderEvent == null)
            {
                _logger.LogWarning("Step 1: FAILED - Deserialization returned null");
                _logger.LogWarning("Message content: {Message}", message);
                return;
            }

            _logger.LogInformation("Step 1: SUCCESS - OrderPaid event deserialized");
            _logger.LogInformation("OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}, PaidDate: {PaidDate}",
                orderEvent.OrderId, orderEvent.CustomerId, orderEvent.TotalAmount, orderEvent.PaidDate);
            _logger.LogInformation("OrderItems count: {ItemCount}", orderEvent.OrderItems?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 1: FAILED - Error deserializing OrderPaid event");
            _logger.LogError("Message: {Message}, Error: {Error}", message, ex.Message);
            throw;
        }

        _logger.LogInformation("Step 2: Creating service scope...");
        using var scope = _serviceProvider.CreateScope();
        var warehouseRepository = scope.ServiceProvider.GetRequiredService<IWarehouseItemRepository>();
        _logger.LogInformation("Step 2: Service scope created, dependencies resolved");

        _logger.LogInformation("Step 3: Processing {ItemCount} order items...", orderEvent.OrderItems?.Count ?? 0);

        int processedItems = 0;
        int failedItems = 0;
        int stockReducedItems = 0;

        foreach (var orderItem in orderEvent.OrderItems ?? new List<OrderItemDto>())
        {
            _logger.LogInformation("--- Processing Order Item {ItemNumber}/{TotalItems} ---",
                processedItems + failedItems + 1, orderEvent.OrderItems?.Count ?? 0);
            _logger.LogInformation("OrderItemId: {OrderItemId}, BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}, UnitPrice: {UnitPrice}",
                orderItem.OrderItemId, orderItem.BookISBN, orderItem.SellerId, orderItem.Quantity, orderItem.UnitPrice);

            try
            {
                _logger.LogInformation("Step 3.{ItemNumber}.1: Looking up warehouse item for BookISBN: {BookISBN}, SellerId: {SellerId}...",
                    processedItems + failedItems + 1, orderItem.BookISBN, orderItem.SellerId);

                var warehouseItem = await warehouseRepository.GetWarehouseItemByBookAndSellerAsync(
                    orderItem.BookISBN,
                    orderItem.SellerId);

                if (warehouseItem == null)
                {
                    _logger.LogWarning("Step 3.{ItemNumber}.1: FAILED - Warehouse item not found", processedItems + failedItems + 1);
                    _logger.LogWarning("BookISBN: {BookISBN}, SellerId: {SellerId}, OrderId: {OrderId}",
                        orderItem.BookISBN, orderItem.SellerId, orderEvent.OrderId);
                    
                    // Try to find any items with the same BookISBN to help debug
                    try
                    {
                        var itemsWithSameBook = await warehouseRepository.GetWarehouseItemsByBookIsbnAsync(orderItem.BookISBN);
                        var itemsList = itemsWithSameBook.ToList();
                        if (itemsList.Any())
                        {
                            _logger.LogWarning("Found {Count} warehouse items with BookISBN {BookISBN}, but different SellerIds:",
                                itemsList.Count, orderItem.BookISBN);
                            foreach (var item in itemsList)
                            {
                                _logger.LogWarning("  - SellerId: '{SellerId}', Quantity: {Quantity}, Price: {Price}",
                                    item.SellerId, item.Quantity, item.Price);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No warehouse items found with BookISBN {BookISBN} at all", orderItem.BookISBN);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check for alternative warehouse items");
                    }
                    
                    failedItems++;
                    continue;
                }

                _logger.LogInformation("Step 3.{ItemNumber}.1: SUCCESS - Warehouse item found", processedItems + failedItems + 1);
                _logger.LogInformation("WarehouseItemId: {Id}, Current Quantity: {CurrentQuantity}, Price: {Price}, Location: {Location}",
                    warehouseItem.Id, warehouseItem.Quantity, warehouseItem.Price, warehouseItem.Location);

                // Reduce quantity
                var oldQuantity = warehouseItem.Quantity;
                _logger.LogInformation("Step 3.{ItemNumber}.2: Reducing quantity from {OldQuantity} by {Reduction}...",
                    processedItems + failedItems + 1, oldQuantity, orderItem.Quantity);

                warehouseItem.Quantity -= orderItem.Quantity;

                // Ensure quantity doesn't go below 0
                if (warehouseItem.Quantity < 0)
                {
                    _logger.LogWarning("Step 3.{ItemNumber}.2: WARNING - Quantity would go negative ({NewQuantity}), setting to 0",
                        processedItems + failedItems + 1, warehouseItem.Quantity);
                    _logger.LogWarning("Requested reduction: {Requested}, Available: {Available}",
                        orderItem.Quantity, oldQuantity);
                    warehouseItem.Quantity = 0;
                }

                var newQuantity = warehouseItem.Quantity;
                _logger.LogInformation("Step 3.{ItemNumber}.2: Quantity updated - Old: {OldQuantity}, New: {NewQuantity}, Reduced: {Reduced}",
                    processedItems + failedItems + 1, oldQuantity, newQuantity, orderItem.Quantity);

                _logger.LogInformation("Step 3.{ItemNumber}.3: Updating warehouse item in database...",
                    processedItems + failedItems + 1);

                var updatedItem = await warehouseRepository.UpdateWarehouseItemAsync(warehouseItem.Id, warehouseItem);

                if (updatedItem == null)
                {
                    _logger.LogError("Step 3.{ItemNumber}.3: FAILED - Update returned null",
                        processedItems + failedItems + 1);
                    failedItems++;
                    continue;
                }

                _logger.LogInformation("Step 3.{ItemNumber}.3: SUCCESS - Warehouse item updated in database",
                    processedItems + failedItems + 1);
                _logger.LogInformation("Final Quantity: {FinalQuantity}", updatedItem.Quantity);

                _logger.LogInformation("Step 3.{ItemNumber}.4: Publishing aggregated BookStockUpdated event...",
                    processedItems + failedItems + 1);

                // Use StockAggregationService to publish aggregated event
                var stockAggregationService = scope.ServiceProvider.GetRequiredService<StockAggregationService>();
                await stockAggregationService.PublishAggregatedStockEventAsync(updatedItem.BookISBN);

                _logger.LogInformation("Step 3.{ItemNumber}.4: SUCCESS - Aggregated BookStockUpdated event published",
                    processedItems + failedItems + 1);

                stockReducedItems++;
                processedItems++;

                _logger.LogInformation("--- Order Item {ItemNumber} Processed Successfully ---",
                    processedItems + failedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step 3.{ItemNumber}: FAILED - Error processing order item",
                    processedItems + failedItems + 1);
                _logger.LogError("BookISBN: {BookISBN}, SellerId: {SellerId}, OrderId: {OrderId}, Error: {Error}",
                    orderItem.BookISBN, orderItem.SellerId, orderEvent.OrderId, ex.Message);
                failedItems++;
                // Continue processing other items even if one fails
            }
        }

        _logger.LogInformation("=== ORDERPAID EVENT PROCESSING COMPLETED ===");
        _logger.LogInformation("OrderId: {OrderId}, Total Items: {TotalItems}, Processed: {Processed}, Stock Reduced: {StockReduced}, Failed: {Failed}",
            orderEvent.OrderId, orderEvent.OrderItems?.Count ?? 0, processedItems, stockReducedItems, failedItems);
    }

    public override void Dispose()
    {
        _logger.LogInformation("=== DISPOSING RABBITMQ CONSUMER ===");
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private class OrderPaidEvent
    {
        public Guid OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime? PaidDate { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new();
    }

    private class OrderItemDto
    {
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

