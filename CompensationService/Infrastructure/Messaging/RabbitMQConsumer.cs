using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using CompensationService.Application.Services;
using CompensationService.Models;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CompensationService.Infrastructure.Messaging;

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
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct, durable: true);

            // Declare DLQ exchange
            _channel.ExchangeDeclare(
                exchange: "book_events.dlq",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare DLQ queue
            var dlqQueueName = _channel.QueueDeclare(
                queue: "compensationservice_queue.dlq",
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
                queue: "compensationservice_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs).QueueName;

            // Bind to failure events
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "InventoryReservationFailed");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "SellerStatsUpdateFailed");
            _channel.QueueBind(queue: _queueName, exchange: "book_events", routingKey: "NotificationFailed");

            _logger.LogInformation("CompensationService RabbitMQ Consumer initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumer");
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
            var routingKey = ea.RoutingKey;
            
            // Log FIRST BYTE before any processing
            var firstByte = body.Length > 0 ? body[0] : (byte)0;
            var firstChar = body.Length > 0 ? (char)body[0] : '?';
            _logger.LogInformation("=== MESSAGE RECEIVED ===");
            _logger.LogInformation("FIRST BYTE: {Byte} (0x{ByteHex:X2}) = '{Char}' (ASCII: {Ascii})", 
                firstByte, firstByte, firstChar, (int)firstChar);
            _logger.LogInformation("RoutingKey: {RoutingKey}", routingKey);
            _logger.LogInformation("BodyLength: {Length}", body.Length);
            _logger.LogInformation("First 20 bytes (hex): {Bytes}", string.Join(" ", body.Take(20).Select(b => b.ToString("X2"))));
            _logger.LogInformation("First 20 bytes (ASCII): {Ascii}", Encoding.ASCII.GetString(body.Take(20).ToArray()));
            
            // Check ContentType
            var contentType = ea.BasicProperties?.ContentType ?? "not set";
            _logger.LogInformation("ContentType: {ContentType}", contentType);
            
            // Try to decode as UTF-8
            string message;
            try
            {
                message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("=== FULL MESSAGE (UTF-8) ===");
                _logger.LogInformation("Message length: {Length}", message.Length);
                _logger.LogInformation("First char: '{FirstChar}' (code: {Code})", 
                    message.Length > 0 ? message[0] : '?', 
                    message.Length > 0 ? (int)message[0] : 0);
                _logger.LogInformation("Full message: {FullMessage}", message);
                
                // Skip JsonDocument.Parse for now - it's causing errors
                // We'll parse in ProcessMessageAsync instead
            }
            catch (Exception decodeEx)
            {
                _logger.LogError(decodeEx, "Failed to decode message as UTF-8");
                _channel.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            try
            {
                await ProcessMessageAsync(routingKey, message);
                _channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation("Successfully processed message with routing key: {RoutingKey}", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with routing key {RoutingKey}", routingKey);
                // Don't requeue - send to DLQ
                _channel.BasicNack(ea.DeliveryTag, false, false);
                _logger.LogWarning("Message NACKed and sent to DLQ (Delivery Tag: {DeliveryTag})", ea.DeliveryTag);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
    {
        _logger.LogInformation("=== PROCESSMESSAGEASYNC START ===");
        _logger.LogInformation("RoutingKey: {RoutingKey}", routingKey);
        _logger.LogInformation("Message length: {Length}", message.Length);
        _logger.LogInformation("First char: '{FirstChar}' (code: {Code})", 
            message.Length > 0 ? message[0] : '?', 
            message.Length > 0 ? (int)message[0] : 0);
        _logger.LogInformation("Full message: {FullMessage}", message);
        
        // Check if message is JSON-wrapped string (starts and ends with quotes)
        // Also check if message doesn't start with '{' (valid JSON object start)
        if (message.Length > 2 && message[0] == '"' && message[message.Length - 1] == '"')
        {
            _logger.LogWarning("Message appears to be JSON-wrapped string. Attempting to unwrap...");
            try
            {
                var unwrapped = JsonSerializer.Deserialize<string>(message);
                if (unwrapped != null && unwrapped.Length > 0 && unwrapped[0] == '{')
                {
                    _logger.LogInformation("Successfully unwrapped double-encoded JSON. Unwrapped: {Unwrapped}", unwrapped);
                    message = unwrapped;
                }
                else
                {
                    _logger.LogWarning("Unwrapped message doesn't start with '{{', keeping original");
                }
            }
            catch (Exception unwrapEx)
            {
                _logger.LogWarning(unwrapEx, "Failed to unwrap message, continuing with original");
            }
        }
        
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<CompensationOrchestrator>();

        switch (routingKey)
        {
            case "InventoryReservationFailed":
                _logger.LogInformation("=== DESERIALIZING InventoryReservationFailedEvent ===");
                _logger.LogInformation("Message to deserialize: {Message}", message);
                
                // Log first byte/char for debugging
                var firstChar = message.Length > 0 ? message[0] : '?';
                var firstCharAscii = message.Length > 0 ? (int)message[0] : 0;
                _logger.LogInformation("FIRST CHAR: '{Char}' (ASCII: {Ascii})", firstChar, firstCharAscii);
                
                // Use PropertyNameCaseInsensitive to handle both camelCase (from tests) and PascalCase (from services)
                InventoryReservationFailedEvent? inventoryFailedEvent = null;
                try
                {
                    // Check if message is double-encoded (starts with quote, indicating JSON string)
                    // Also check if message doesn't start with '{' (valid JSON object start)
                    if (message.Length > 0 && message[0] != '{')
                    {
                        _logger.LogWarning("Message doesn't start with '{{'. First char: '{Char}' (ASCII: {Ascii}). Attempting to unwrap...", firstChar, firstCharAscii);
                        try
                        {
                            // Try to deserialize as string first (if it's double-encoded JSON string)
                            var jsonString = JsonSerializer.Deserialize<string>(message);
                            if (jsonString != null && jsonString.Length > 0 && jsonString[0] == '{')
                            {
                                _logger.LogInformation("Successfully unwrapped double-encoded JSON. Unwrapped: {Unwrapped}", jsonString);
                                message = jsonString;
                            }
                            else
                            {
                                _logger.LogWarning("Unwrapped message doesn't start with '{{', first char: '{Char}'", 
                                    jsonString != null && jsonString.Length > 0 ? jsonString[0] : '?');
                            }
                        }
                        catch (Exception unwrapEx)
                        {
                            _logger.LogWarning(unwrapEx, "Failed to unwrap as string, trying direct deserialization");
                        }
                    }
                    
                    // Final check - message must start with '{' for valid JSON object
                    if (message.Length == 0 || message[0] != '{')
                    {
                        throw new JsonException($"Invalid JSON message format. Expected to start with '{{', but first char is: '{firstChar}' (ASCII: {firstCharAscii}). Message: {message.Substring(0, Math.Min(100, message.Length))}");
                    }
                    
                    inventoryFailedEvent = JsonSerializer.Deserialize<InventoryReservationFailedEvent>(message, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    _logger.LogInformation("Successfully deserialized InventoryReservationFailedEvent. OrderId: {OrderId}", 
                        inventoryFailedEvent?.OrderId ?? Guid.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize InventoryReservationFailedEvent. First char: '{FirstChar}' (ASCII: {Ascii}). Full message: {FullMessage}", 
                        firstChar, firstCharAscii, message);
                    throw; // Re-throw to trigger DLQ
                }
                
                if (inventoryFailedEvent != null)
                {
                    _logger.LogInformation("Calling HandleInventoryReservationFailed for OrderId: {OrderId}", inventoryFailedEvent.OrderId);
                    orchestrator.HandleInventoryReservationFailed(inventoryFailedEvent);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize InventoryReservationFailedEvent - message was null after all attempts");
                }
                break;

            case "SellerStatsUpdateFailed":
                var sellerStatsFailedEvent = JsonSerializer.Deserialize<SellerStatsUpdateFailedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (sellerStatsFailedEvent != null)
                {
                    orchestrator.HandleSellerStatsUpdateFailed(sellerStatsFailedEvent);
                }
                break;

            case "NotificationFailed":
                var notificationFailedEvent = JsonSerializer.Deserialize<NotificationFailedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (notificationFailedEvent != null)
                {
                    orchestrator.HandleNotificationFailed(notificationFailedEvent);
                }
                break;
        }

        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}

