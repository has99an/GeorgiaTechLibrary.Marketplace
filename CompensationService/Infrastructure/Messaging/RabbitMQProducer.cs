using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace CompensationService.Infrastructure.Messaging;

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
        try
        {
            _logger.LogInformation("=== RABBITMQ PRODUCER: SENDING MESSAGE ===");
            _logger.LogInformation("Message type: {MessageType}, Routing key: {RoutingKey}", typeof(T).Name, routingKey);
            
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            
            _logger.LogInformation("Serialized message length: {Length} bytes", body.Length);
            _logger.LogInformation("Message content: {Content}", json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "book_events",
                routingKey: routingKey,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation("=== MESSAGE PUBLISHED SUCCESSFULLY ===");
            _logger.LogInformation("Published to exchange 'book_events' with routing key '{RoutingKey}'", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to RabbitMQ");
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

