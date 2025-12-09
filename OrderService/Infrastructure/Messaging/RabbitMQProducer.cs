using System.Text;
using System.Text.Json;
using OrderService.Application.Interfaces;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

public class RabbitMQProducer : IMessageProducer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQProducer> _logger;

    public RabbitMQProducer(IConfiguration configuration, ILogger<RabbitMQProducer> logger)
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

            _logger.LogInformation("Connected to RabbitMQ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public void SendMessage<T>(T message, string routingKey)
    {
        _logger.LogInformation("=== RABBITMQ PRODUCER: SENDING MESSAGE ===");
        _logger.LogInformation("Routing Key: {RoutingKey}", routingKey);

        try
        {
            _logger.LogInformation("Step 1: Checking RabbitMQ connection status...");
            if (_connection == null || !_connection.IsOpen)
            {
                _logger.LogError("Step 1: FAILED - RabbitMQ connection is not open");
                throw new InvalidOperationException("RabbitMQ connection is not open");
            }
            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogError("Step 1: FAILED - RabbitMQ channel is closed");
                throw new InvalidOperationException("RabbitMQ channel is closed");
            }
            _logger.LogInformation("Step 1: SUCCESS - Connection and channel are open");

            _logger.LogInformation("Step 2: Serializing message to JSON...");
            var json = JsonSerializer.Serialize(message);
            _logger.LogInformation("Step 2: SUCCESS - Message serialized, JSON length: {JsonLength}", json.Length);
            _logger.LogInformation("Step 2: JSON content: {Json}", json);

            _logger.LogInformation("Step 3: Converting JSON to bytes...");
            var body = Encoding.UTF8.GetBytes(json);
            _logger.LogInformation("Step 3: SUCCESS - Converted to {BodyLength} bytes", body.Length);

            _logger.LogInformation("Step 4: Creating message properties...");
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _logger.LogInformation("Step 4: SUCCESS - Properties created (Persistent: {Persistent}, ContentType: {ContentType})",
                properties.Persistent, properties.ContentType);

            _logger.LogInformation("Step 5: Publishing message to RabbitMQ...");
            _logger.LogInformation("Step 5: Exchange: 'book_events', RoutingKey: '{RoutingKey}'", routingKey);
            _channel.BasicPublish(
                exchange: "book_events",
                routingKey: routingKey,
                basicProperties: properties,
                body: body
            );
            _logger.LogInformation("Step 5: SUCCESS - Message published to RabbitMQ");
            _logger.LogInformation("=== MESSAGE SENT SUCCESSFULLY ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== FAILED TO SEND MESSAGE TO RABBITMQ ===");
            _logger.LogError("Routing Key: {RoutingKey}, Error: {Error}", routingKey, ex.Message);
            throw;
        }
    }

    public Task SendMessageAsync<T>(T message, string routingKey)
    {
        SendMessage(message, routingKey);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

