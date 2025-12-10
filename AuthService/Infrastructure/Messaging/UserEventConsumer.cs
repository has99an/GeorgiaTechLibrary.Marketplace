using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AuthService.Application.Interfaces;

namespace AuthService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ consumer for handling UserUpdated and UserRoleChanged events from UserService
/// </summary>
public class UserEventConsumer : BackgroundService
{
    private readonly ILogger<UserEventConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IModel? _channel;

    public UserEventConsumer(
        ILogger<UserEventConsumer> logger,
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
                _logger.LogInformation("Attempting to connect to RabbitMQ for User events (attempt {Attempt}/{MaxRetries})...", 
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
                        queue: "auth_service_user_queue",
                        autoAck: false,
                        consumer: consumer);

                    _logger.LogInformation("User event consumer started. Listening for UserUpdated and UserRoleChanged events...");
                    
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

            // Declare exchange (should match UserService exchange name)
            _channel.ExchangeDeclare(
                exchange: "user_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare queue
            _channel.QueueDeclare(
                queue: "auth_service_user_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind queue to exchange for UserUpdated and UserRoleChanged events
            _channel.QueueBind(
                queue: "auth_service_user_queue",
                exchange: "user_events",
                routingKey: "UserUpdated");
            
            _channel.QueueBind(
                queue: "auth_service_user_queue",
                exchange: "user_events",
                routingKey: "UserRoleChanged");

            _logger.LogInformation("User event consumer initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize User event consumer");
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

            _logger.LogInformation("Received user event message with routing key: {RoutingKey}", routingKey);

            if (routingKey == "UserUpdated" || routingKey == "UserRoleChanged")
            {
                await HandleUserUpdatedAsync(message, cancellationToken);
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
            _logger.LogError(ex, "Error handling user event message. RoutingKey: {RoutingKey}, Error: {Error}", 
                ea.RoutingKey, ex.Message);
            
            // Reject and requeue the message
            _channel?.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task HandleUserUpdatedAsync(string message, CancellationToken cancellationToken)
    {
        UserUpdatedEvent? userEvent = null;
        try
        {
            userEvent = JsonSerializer.Deserialize<UserUpdatedEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (userEvent == null)
            {
                _logger.LogError("Failed to deserialize UserUpdated event. Message: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing UserUpdated event - UserId: {UserId}, Role: {Role}", 
                userEvent.UserId, userEvent.Role);

            // Create service scope
            using var scope = _serviceProvider.CreateScope();
            var authUserRepository = scope.ServiceProvider.GetRequiredService<IAuthUserRepository>();

            // Get auth user
            var authUser = await authUserRepository.GetAuthUserByIdAsync(userEvent.UserId, cancellationToken);
            if (authUser == null)
            {
                _logger.LogWarning("AuthUser not found for role update: {UserId}", userEvent.UserId);
                return;
            }

            // Update role if provided and different
            if (!string.IsNullOrWhiteSpace(userEvent.Role) && authUser.Role != userEvent.Role)
            {
                authUser.UpdateRole(userEvent.Role);
                await authUserRepository.UpdateAuthUserAsync(authUser, cancellationToken);

                _logger.LogInformation("AuthUser role updated: {UserId}, NewRole: {Role}", 
                    userEvent.UserId, userEvent.Role);
            }
            else
            {
                _logger.LogInformation("AuthUser role unchanged or not provided: {UserId}, CurrentRole: {CurrentRole}, EventRole: {EventRole}", 
                    userEvent.UserId, authUser.Role, userEvent.Role);
            }

            _logger.LogInformation("UserUpdated event processed successfully - UserId: {UserId}", userEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserUpdated event - UserId: {UserId}", 
                userEvent?.UserId ?? Guid.Empty);
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
    private class UserUpdatedEvent
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? EventType { get; set; }
    }
}

