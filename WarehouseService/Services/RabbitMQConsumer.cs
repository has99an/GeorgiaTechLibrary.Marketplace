using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WarehouseService.Repositories;
using WarehouseService.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WarehouseService.Services;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string? _queueName;

    public RabbitMQConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQConsumer> logger)
    {
        // Initialize these first to avoid nullable warnings
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        try
        {

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

            _logger.LogInformation("Step 5.5: Declaring DLQ exchange 'book_events.dlq'...");
            _channel.ExchangeDeclare(
                exchange: "book_events.dlq",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);
            _logger.LogInformation("Step 5.5: DLQ exchange 'book_events.dlq' declared successfully");

            _logger.LogInformation("Step 5.6: Declaring DLQ queue 'warehouseservice_order_queue.dlq'...");
            var dlqQueueName = _channel.QueueDeclare(
                queue: "warehouseservice_order_queue.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false).QueueName;
            _channel.QueueBind(queue: dlqQueueName, exchange: "book_events.dlq", routingKey: "failed");
            _logger.LogInformation("Step 5.6: DLQ queue '{DlqQueueName}' declared and bound successfully", dlqQueueName);

            _logger.LogInformation("Step 6: Declaring queue 'warehouseservice_order_queue' with DLQ...");
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "book_events.dlq" },
                { "x-dead-letter-routing-key", "failed" }
            };
            _queueName = _channel.QueueDeclare(
                queue: "warehouseservice_order_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs).QueueName;
            _logger.LogInformation("Step 6: Queue '{QueueName}' declared successfully with DLQ configuration", _queueName);

            _logger.LogInformation("Step 7: Binding queue to exchange with routing keys...");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "OrderPaid");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "BookAddedForSale");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "CompensateInventoryReservation");
            _logger.LogInformation("Step 7: Queue bound successfully to 'book_events' exchange with routing keys 'OrderPaid', 'BookAddedForSale', and 'CompensateInventoryReservation'");

            _logger.LogInformation("=== WAREHOUSE RABBITMQ CONSUMER INITIALIZED SUCCESSFULLY ===");
            _logger.LogInformation("Ready to receive OrderPaid and BookAddedForSale events from RabbitMQ");
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

        if (_channel == null || _queueName == null)
        {
            _logger.LogError("Channel or queue name is null. Cannot start consumer.");
            return Task.CompletedTask;
        }

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
                if (_channel != null)
                {
                    _channel.BasicAck(deliveryTag, false);
                    _logger.LogInformation("Message acknowledged successfully (Delivery Tag: {DeliveryTag})", deliveryTag);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR PROCESSING MESSAGE ===");
                _logger.LogError("Routing Key: {RoutingKey}, Delivery Tag: {DeliveryTag}, Error: {Error}",
                    routingKey, deliveryTag, ex.Message);
                // Don't requeue - send to DLQ after max retries
                // Individual item failures are handled internally with failure events
                if (_channel != null)
                {
                    _channel.BasicNack(deliveryTag, false, false); // Don't requeue - goes to DLQ
                    _logger.LogWarning("Message NACKed and sent to DLQ (Delivery Tag: {DeliveryTag})", deliveryTag);
                }
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
        else if (routingKey == "BookAddedForSale")
        {
            _logger.LogInformation("Routing key matches 'BookAddedForSale' - processing book listing...");
            await HandleBookAddedForSaleAsync(message);
        }
        else if (routingKey == "CompensateInventoryReservation")
        {
            _logger.LogInformation("Routing key matches 'CompensateInventoryReservation' - processing compensation...");
            await HandleCompensateInventoryReservationAsync(message);
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

        // Extra validation: Check if seller is trying to buy their own books (defensive check)
        _logger.LogInformation("Step 1.5: Validating seller cannot buy own books (defensive check)...");
        var conflictingItems = orderEvent.OrderItems?
            .Where(item => item.SellerId.Equals(orderEvent.CustomerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (conflictingItems != null && conflictingItems.Any())
        {
            _logger.LogError("Step 1.5: FAILED - Seller {CustomerId} attempted to buy their own books. This should have been blocked in OrderService!", orderEvent.CustomerId);
            _logger.LogError("OrderId: {OrderId}, Conflicting items: {ItemCount}", orderEvent.OrderId, conflictingItems.Count);
            // Log error but don't throw - this is a defensive check and order was already created
            // In production, you might want to raise an alert or notification
        }
        else
        {
            _logger.LogInformation("Step 1.5: SUCCESS - No seller self-purchase detected (defensive check passed)");
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
                await RetryPolicy.ExecuteWithRetryAsync(async () =>
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
                        
                        throw new InvalidOperationException($"Warehouse item not found for BookISBN: {orderItem.BookISBN}, SellerId: {orderItem.SellerId}");
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
                        throw new InvalidOperationException($"Failed to update warehouse item with ID: {warehouseItem.Id}");
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

                    return true;
                }, maxRetries: 3, logger: _logger);

                stockReducedItems++;
                processedItems++;

                _logger.LogInformation("--- Order Item {ItemNumber} Processed Successfully ---",
                    processedItems + failedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step 3.{ItemNumber}: FAILED - Error processing order item after all retries",
                    processedItems + failedItems + 1);
                _logger.LogError("BookISBN: {BookISBN}, SellerId: {SellerId}, OrderId: {OrderId}, Error: {Error}",
                    orderItem.BookISBN, orderItem.SellerId, orderEvent.OrderId, ex.Message);
                
                // Publish failure event
                try
                {
                    var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();
                    var failureEvent = new
                    {
                        OrderId = orderEvent.OrderId,
                        OrderItemId = orderItem.OrderItemId,
                        BookISBN = orderItem.BookISBN,
                        SellerId = orderItem.SellerId,
                        Quantity = orderItem.Quantity,
                        ErrorMessage = ex.Message,
                        FailedAt = DateTime.UtcNow,
                        RetryAttempts = 3
                    };
                    messageProducer.SendMessage(failureEvent, "InventoryReservationFailed");
                    _logger.LogInformation("Published InventoryReservationFailed event for OrderItemId: {OrderItemId}", orderItem.OrderItemId);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, "Failed to publish InventoryReservationFailed event");
                }
                
                failedItems++;
                // Continue processing other items even if one fails
            }
        }

        _logger.LogInformation("=== ORDERPAID EVENT PROCESSING COMPLETED ===");
        _logger.LogInformation("OrderId: {OrderId}, Total Items: {TotalItems}, Processed: {Processed}, Stock Reduced: {StockReduced}, Failed: {Failed}",
            orderEvent.OrderId, orderEvent.OrderItems?.Count ?? 0, processedItems, stockReducedItems, failedItems);
    }

    private async Task HandleBookAddedForSaleAsync(string message)
    {
        _logger.LogInformation("=== HANDLING BOOKADDEDFORSALE EVENT ===");
        _logger.LogInformation("Step 1: Deserializing BookAddedForSale event...");
        _logger.LogInformation("Raw message: {Message}", message);

        BookAddedForSaleEvent? bookEvent = null;
        try
        {
            bookEvent = JsonSerializer.Deserialize<BookAddedForSaleEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bookEvent == null)
            {
                _logger.LogWarning("Step 1: FAILED - Deserialization returned null");
                _logger.LogWarning("Message content: {Message}", message);
                return;
            }

            _logger.LogInformation("Step 1: SUCCESS - BookAddedForSale event deserialized");
            _logger.LogInformation("ListingId: {ListingId}, SellerId: {SellerId}, BookISBN: {BookISBN}, Quantity: {Quantity}, Price: {Price}, Condition: {Condition}, CreatedDate: {CreatedDate}",
                bookEvent.ListingId, bookEvent.SellerId, bookEvent.BookISBN, bookEvent.Quantity, bookEvent.Price, bookEvent.Condition, bookEvent.CreatedDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 1: FAILED - Error deserializing BookAddedForSale event");
            _logger.LogError("Message: {Message}, Error: {Error}", message, ex.Message);
            throw;
        }

        _logger.LogInformation("Step 2: Creating service scope...");
        using var scope = _serviceProvider.CreateScope();
        var warehouseRepository = scope.ServiceProvider.GetRequiredService<IWarehouseItemRepository>();
        var stockAggregationService = scope.ServiceProvider.GetRequiredService<StockAggregationService>();
        _logger.LogInformation("Step 2: Service scope created, dependencies resolved");

        // Convert SellerId from Guid to string
        var sellerIdString = bookEvent.SellerId.ToString();
        
        // Map Condition to IsNew: "New" = true, all others = false
        var isNew = bookEvent.Condition.Equals("New", StringComparison.OrdinalIgnoreCase);
        
        // Default location for seller listings
        var location = "Student Seller";

        _logger.LogInformation("Step 3: Checking if WarehouseItem already exists for BookISBN: {BookISBN}, SellerId: {SellerId}...",
            bookEvent.BookISBN, sellerIdString);

        // Check if WarehouseItem already exists for this SellerId + BookISBN combination
        var existingItem = await warehouseRepository.GetWarehouseItemByBookAndSellerAsync(
            bookEvent.BookISBN, 
            sellerIdString);

        if (existingItem != null)
        {
            // Update existing item
            _logger.LogInformation("Step 3: SUCCESS - WarehouseItem found: Id={Id}", existingItem.Id);
            _logger.LogInformation("Step 4: Updating existing WarehouseItem...");
            _logger.LogInformation("Current values - Quantity: {OldQuantity}, Price: {OldPrice}, IsNew: {OldIsNew}",
                existingItem.Quantity, existingItem.Price, existingItem.IsNew);
            _logger.LogInformation("New values - Quantity: {NewQuantity}, Price: {NewPrice}, IsNew: {NewIsNew}",
                bookEvent.Quantity, bookEvent.Price, isNew);
            
            existingItem.Quantity = bookEvent.Quantity;
            existingItem.Price = bookEvent.Price;
            existingItem.IsNew = isNew;
            // Location can be updated if needed, but keeping existing for now
            
            var updatedItem = await warehouseRepository.UpdateWarehouseItemAsync(existingItem.Id, existingItem);
            
            if (updatedItem == null)
            {
                _logger.LogError("Step 4: FAILED - Update returned null");
                return;
            }
            
            _logger.LogInformation("Step 4: SUCCESS - WarehouseItem updated: Id={Id}, Quantity={Quantity}, Price={Price}, IsNew={IsNew}",
                updatedItem.Id, updatedItem.Quantity, updatedItem.Price, updatedItem.IsNew);
        }
        else
        {
            // Create new WarehouseItem
            _logger.LogInformation("Step 3: No existing WarehouseItem found - creating new item");
            _logger.LogInformation("Step 4: Creating new WarehouseItem...");
            _logger.LogInformation("Values - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}, Price: {Price}, Location: {Location}, IsNew: {IsNew}",
                bookEvent.BookISBN, sellerIdString, bookEvent.Quantity, bookEvent.Price, location, isNew);
            
            var newItem = new WarehouseItem
            {
                BookISBN = bookEvent.BookISBN,
                SellerId = sellerIdString,
                Quantity = bookEvent.Quantity,
                Price = bookEvent.Price,
                Location = location,
                IsNew = isNew
            };

            var createdItem = await warehouseRepository.AddWarehouseItemAsync(newItem);
            _logger.LogInformation("Step 4: SUCCESS - WarehouseItem created: Id={Id}, BookISBN={BookISBN}, SellerId={SellerId}",
                createdItem.Id, createdItem.BookISBN, createdItem.SellerId);
        }

        _logger.LogInformation("Step 5: Publishing aggregated BookStockUpdated event for ISBN: {BookISBN}...", bookEvent.BookISBN);
        
        // Publish aggregated BookStockUpdated event
        await stockAggregationService.PublishAggregatedStockEventAsync(bookEvent.BookISBN);
        
        _logger.LogInformation("Step 5: SUCCESS - BookStockUpdated event published");
        _logger.LogInformation("=== BOOKADDEDFORSALE EVENT PROCESSING COMPLETED ===");
        _logger.LogInformation("ListingId: {ListingId}, BookISBN: {BookISBN}, SellerId: {SellerId}",
            bookEvent.ListingId, bookEvent.BookISBN, bookEvent.SellerId);
    }

    private async Task HandleCompensateInventoryReservationAsync(string message)
    {
        _logger.LogInformation("=== HANDLING COMPENSATE INVENTORY RESERVATION EVENT ===");
        
        CompensateInventoryReservationEvent? compensationEvent = null;
        try
        {
            compensationEvent = JsonSerializer.Deserialize<CompensateInventoryReservationEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (compensationEvent == null)
            {
                _logger.LogWarning("Failed to deserialize CompensateInventoryReservation event. Message: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing compensation - OrderId: {OrderId}, OrderItemId: {OrderItemId}, BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}",
                compensationEvent.OrderId, compensationEvent.OrderItemId, compensationEvent.BookISBN, compensationEvent.SellerId, compensationEvent.Quantity);

            using var scope = _serviceProvider.CreateScope();
            var warehouseRepository = scope.ServiceProvider.GetRequiredService<IWarehouseItemRepository>();
            var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();

            // Lookup warehouse item
            var warehouseItem = await warehouseRepository.GetWarehouseItemByBookAndSellerAsync(
                compensationEvent.BookISBN,
                compensationEvent.SellerId);

            if (warehouseItem == null)
            {
                _logger.LogWarning("Warehouse item not found for compensation - BookISBN: {BookISBN}, SellerId: {SellerId}",
                    compensationEvent.BookISBN, compensationEvent.SellerId);
                
                // Publish compensation completed event with failure
                var failureEvent = new
                {
                    OrderId = compensationEvent.OrderId,
                    OrderItemId = compensationEvent.OrderItemId,
                    CompensationType = "InventoryReservation",
                    CompletedAt = DateTime.UtcNow,
                    Success = false,
                    ErrorMessage = $"Warehouse item not found for BookISBN: {compensationEvent.BookISBN}, SellerId: {compensationEvent.SellerId}"
                };
                messageProducer.SendMessage(failureEvent, "CompensationCompleted");
                return;
            }

            // Restore stock quantity (add back what was reduced)
            var oldQuantity = warehouseItem.Quantity;
            warehouseItem.Quantity += compensationEvent.Quantity;
            _logger.LogInformation("Restoring stock - Old: {OldQuantity}, Restored: {Restored}, New: {NewQuantity}",
                oldQuantity, compensationEvent.Quantity, warehouseItem.Quantity);

            var updatedItem = await warehouseRepository.UpdateWarehouseItemAsync(warehouseItem.Id, warehouseItem);

            if (updatedItem == null)
            {
                _logger.LogError("Failed to update warehouse item during compensation");
                var failureEvent = new
                {
                    OrderId = compensationEvent.OrderId,
                    OrderItemId = compensationEvent.OrderItemId,
                    CompensationType = "InventoryReservation",
                    CompletedAt = DateTime.UtcNow,
                    Success = false,
                    ErrorMessage = "Failed to update warehouse item"
                };
                messageProducer.SendMessage(failureEvent, "CompensationCompleted");
                return;
            }

            // Publish BookStockUpdated event
            var stockAggregationService = scope.ServiceProvider.GetRequiredService<StockAggregationService>();
            await stockAggregationService.PublishAggregatedStockEventAsync(updatedItem.BookISBN);

            // Publish compensation completed event
            var successEvent = new
            {
                OrderId = compensationEvent.OrderId,
                OrderItemId = compensationEvent.OrderItemId,
                CompensationType = "InventoryReservation",
                CompletedAt = DateTime.UtcNow,
                Success = true,
                ErrorMessage = (string?)null
            };
            messageProducer.SendMessage(successEvent, "CompensationCompleted");

            _logger.LogInformation("Compensation completed successfully - OrderId: {OrderId}, OrderItemId: {OrderItemId}",
                compensationEvent.OrderId, compensationEvent.OrderItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing compensation event");
            if (compensationEvent != null)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var messageProducer = scope.ServiceProvider.GetRequiredService<IMessageProducer>();
                    var failureEvent = new
                    {
                        OrderId = compensationEvent.OrderId,
                        OrderItemId = compensationEvent.OrderItemId,
                        CompensationType = "InventoryReservation",
                        CompletedAt = DateTime.UtcNow,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                    messageProducer.SendMessage(failureEvent, "CompensationCompleted");
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, "Failed to publish compensation failure event");
                }
            }
        }
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

    /// <summary>
    /// Event DTO for BookAddedForSale event (matches UserService.BookAddedForSaleEventDto structure)
    /// </summary>
    private class BookAddedForSaleEvent
    {
        public Guid ListingId { get; set; }
        public Guid SellerId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Condition { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// Event DTO for compensation request
    /// </summary>
    private class CompensateInventoryReservationEvent
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}

