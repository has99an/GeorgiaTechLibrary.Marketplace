using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace UserService.Services;

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
            _channel.ExchangeDeclare(exchange: "user_events", type: ExchangeType.Direct);

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
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: "user_events",
                routingKey: routingKey,
                basicProperties: null,
                body: body
            );

            _logger.LogInformation("Message sent to RabbitMQ with routing key: {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to RabbitMQ");
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
