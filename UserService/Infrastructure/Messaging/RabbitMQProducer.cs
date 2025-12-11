using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ message producer implementation
/// </summary>
public class RabbitMQProducer : IMessageProducer, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMQProducer> _logger;
    private readonly bool _isConnected;

    public RabbitMQProducer(IConfiguration configuration, ILogger<RabbitMQProducer> logger)
    {
        _logger = logger;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:Username"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchanges
            _channel.ExchangeDeclare(
                exchange: "user_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);
            
            // Declare book_events exchange for BookAddedForSale events
            _channel.ExchangeDeclare(
                exchange: "book_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            _isConnected = true;
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", 
                factory.HostName, factory.Port);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogError(ex, "Failed to connect to RabbitMQ. Message publishing will be disabled.");
        }
    }

    public void SendMessage<T>(T message, string routingKey)
    {
        if (!_isConnected || _channel == null)
        {
            _logger.LogWarning("RabbitMQ not connected. Message not sent: {RoutingKey}", routingKey);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Determine exchange based on routing key
            // BookAddedForSale, BookSold, and BookStockUpdated events go to book_events exchange, all others go to user_events
            var exchange = (routingKey == "BookAddedForSale" || routingKey == "BookSold" || routingKey == "BookStockUpdated") ? "book_events" : "user_events";

            _channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Message published to RabbitMQ: Exchange={Exchange}, RoutingKey={RoutingKey}", exchange, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ: {RoutingKey}", routingKey);
        }
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ connection closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
        }
    }
}

