using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Tests.Shared;

namespace Tests.Shared;

/// <summary>
/// Helper class for RabbitMQ testing operations
/// </summary>
public class RabbitMQTestHelper : IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly List<string> _queues = new();
    private readonly List<string> _exchanges = new();

    public RabbitMQTestHelper(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    /// <summary>
    /// Declares an exchange for testing
    /// </summary>
    public void DeclareExchange(string exchangeName, string exchangeType = ExchangeType.Direct)
    {
        _channel.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: true);
        _exchanges.Add(exchangeName);
    }

    /// <summary>
    /// Creates a queue and binds it to an exchange
    /// </summary>
    public string CreateQueueAndBind(string exchangeName, string routingKey)
    {
        var queueName = _channel.QueueDeclare().QueueName;
        _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: routingKey);
        _queues.Add(queueName);
        return queueName;
    }

    /// <summary>
    /// Publishes a message to an exchange
    /// </summary>
    public void PublishMessage<T>(T message, string exchangeName, string routingKey)
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

        _channel.BasicPublish(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);
    }

    /// <summary>
    /// Consumes messages from a queue
    /// </summary>
    public List<T> ConsumeMessages<T>(string queueName, int expectedCount, TimeSpan timeout)
    {
        var messages = new List<T>();
        var startTime = DateTime.UtcNow;

        while (messages.Count < expectedCount && DateTime.UtcNow - startTime < timeout)
        {
            var result = _channel.BasicGet(queue: queueName, autoAck: true);
            if (result != null)
            {
                var body = Encoding.UTF8.GetString(result.Body.ToArray());
                var message = JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (message != null)
                {
                    messages.Add(message);
                }
            }
            else
            {
                Thread.Sleep(100); // Wait a bit before checking again
            }
        }

        return messages;
    }

    /// <summary>
    /// Purges all queues created by this helper
    /// </summary>
    public void PurgeQueues()
    {
        foreach (var queue in _queues)
        {
            _channel.QueuePurge(queue);
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

