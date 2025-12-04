using System.Text;
using System.Text.Json;
using System.Threading;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AuthService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ message producer implementation with robust connection handling
/// </summary>
public class RabbitMQProducer : IMessageProducer, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitMQProducer> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _lockObject = new object();
    private bool _isConnected = false;

    public RabbitMQProducer(IConfiguration configuration, ILogger<RabbitMQProducer> logger)
    {
        _logger = logger;
        _configuration = configuration;
        // Don't connect in constructor - use lazy initialization
        // This allows the application to start even if RabbitMQ is not ready
        _logger.LogInformation("RabbitMQProducer initialized. Connection will be established on first message send.");
    }

    private bool InitializeRabbitMQWithRetry(IConfiguration configuration)
    {
        const int maxRetries = 10;
        const int delaySeconds = 2;
        
        // Log configuration source for debugging
        var hostFromConfig = configuration["RabbitMQ:Host"];
        var hostFromEnv = Environment.GetEnvironmentVariable("RabbitMQ__Host");
        _logger.LogInformation("=== RABBITMQ CONFIGURATION DEBUG ===");
        _logger.LogInformation("Configuration source - appsettings: '{HostFromConfig}', Environment: '{HostFromEnv}'", 
            hostFromConfig ?? "null", hostFromEnv ?? "null");
        
        var hostName = hostFromEnv ?? hostFromConfig ?? "localhost";
        var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        var userName = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        _logger.LogInformation("Final RabbitMQ connection settings - Host: '{Host}', Port: {Port}, Username: '{Username}'", 
            hostName, port, userName);
        _logger.LogInformation("Starting RabbitMQ connection with {MaxRetries} retries to {Host}:{Port}", maxRetries, hostName, port);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ (attempt {Attempt}/{MaxRetries}) at {Host}:{Port}", 
                    attempt, maxRetries, hostName, port);

                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName,
                    Password = password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
                    SocketReadTimeout = TimeSpan.FromSeconds(10),
                    SocketWriteTimeout = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange
                _channel.ExchangeDeclare(
                    exchange: "user_events",
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("Successfully connected to RabbitMQ at {Host}:{Port}", hostName, port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries}): {Error}", 
                    attempt, maxRetries, ex.Message);
                
                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay} seconds...", delaySeconds);
                    Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                }
                else
                {
                    _logger.LogError("Failed to connect to RabbitMQ after {MaxRetries} attempts. Message publishing will be disabled.", maxRetries);
                    return false;
                }
            }
        }

        return false;
    }

    public void SendMessage<T>(T message, string routingKey)
    {
        // Always check connection status and reconnect if needed
        bool needsConnection = false;
        lock (_lockObject)
        {
            needsConnection = !_isConnected || _channel == null || _channel.IsClosed;
        }

        if (needsConnection)
        {
            _logger.LogInformation("RabbitMQ not connected. Attempting to connect for routing key: {RoutingKey}...", routingKey);
            
            bool connected = false;
            try
            {
                connected = InitializeRabbitMQWithRetry(_configuration);
                
                lock (_lockObject)
                {
                    _isConnected = connected;
                }
                
                if (!connected)
                {
                    _logger.LogWarning("RabbitMQ not connected after retry. Message not sent: {RoutingKey}", routingKey);
                    return;
                }
                
                _logger.LogInformation("RabbitMQ connection established successfully for routing key: {RoutingKey}", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during RabbitMQ connection attempt for routing key: {RoutingKey}. Error: {Error}", routingKey, ex.Message);
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                return;
            }
        }

        // Double-check channel is valid before publishing
        IModel? channelToUse = null;
        lock (_lockObject)
        {
            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogWarning("Channel is null or closed after connection. Marking as disconnected for routing key: {RoutingKey}", routingKey);
                _isConnected = false;
                return;
            }
            channelToUse = _channel;
        }

        try
        {
            _logger.LogInformation("=== RABBITMQ PRODUCER: SENDING MESSAGE ===");
            _logger.LogInformation("RoutingKey: {RoutingKey}, MessageType: {MessageType}", routingKey, message?.GetType().Name);
            
            _logger.LogInformation("Step 1: Serializing message to JSON...");
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            _logger.LogInformation("Step 1: Message serialized. JSON length: {Length} bytes, Content: {Json}", json.Length, json);
            
            _logger.LogInformation("Step 2: Converting JSON to bytes...");
            var body = Encoding.UTF8.GetBytes(json);
            _logger.LogInformation("Step 2: Message converted to bytes. Body length: {Length} bytes", body.Length);

            _logger.LogInformation("Step 3: Creating message properties...");
            var properties = channelToUse.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _logger.LogInformation("Step 3: Message properties created - Persistent: {Persistent}, ContentType: {ContentType}, Timestamp: {Timestamp}",
                properties.Persistent, properties.ContentType, properties.Timestamp);

            _logger.LogInformation("Step 4: Publishing message to RabbitMQ...");
            _logger.LogInformation("Step 4: Exchange: 'user_events', RoutingKey: '{RoutingKey}'", routingKey);
            channelToUse.BasicPublish(
                exchange: "user_events",
                routingKey: routingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Step 4: BasicPublish call completed");
            _logger.LogInformation("=== MESSAGE PUBLISHED TO RABBITMQ SUCCESSFULLY ===");
            _logger.LogInformation("RoutingKey: {RoutingKey}, Exchange: user_events", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ: {RoutingKey}. Error: {Error}", routingKey, ex.Message);
            
            // Mark as disconnected and close connections to allow retry
            lock (_lockObject)
            {
                _isConnected = false;
                try
                {
                    _channel?.Close();
                    _channel?.Dispose();
                    _connection?.Close();
                    _connection?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                _channel = null;
                _connection = null;
            }
            
            // Try to reconnect once more and retry publish
            _logger.LogInformation("Attempting to reconnect and retry publishing for routing key: {RoutingKey}...", routingKey);
            try
            {
                var hostName = _configuration["RabbitMQ:Host"] ?? "localhost";
                var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672");
                var userName = _configuration["RabbitMQ:Username"] ?? "guest";
                var password = _configuration["RabbitMQ:Password"] ?? "guest";
                
                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName,
                    Password = password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
                    SocketReadTimeout = TimeSpan.FromSeconds(10),
                    SocketWriteTimeout = TimeSpan.FromSeconds(10)
                };
                
                var tempConnection = factory.CreateConnection();
                var tempChannel = tempConnection.CreateModel();
                tempChannel.ExchangeDeclare("user_events", ExchangeType.Direct, true, false);
                
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var body = Encoding.UTF8.GetBytes(json);
                var properties = tempChannel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                
                tempChannel.BasicPublish("user_events", routingKey, properties, body);
                
                // Update main connection if retry succeeded
                lock (_lockObject)
                {
                    _connection = tempConnection;
                    _channel = tempChannel;
                    _isConnected = true;
                }
                
                _logger.LogInformation("Message published to RabbitMQ after reconnection: {RoutingKey}", routingKey);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Failed to publish message after reconnection: {RoutingKey}. Error: {Error}", routingKey, retryEx.Message);
            }
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
