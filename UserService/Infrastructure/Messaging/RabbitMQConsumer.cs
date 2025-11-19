using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UserService.Application.Interfaces;
using UserService.Domain.ValueObjects;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ consumer for handling user events from AuthService
/// </summary>
public class RabbitMQConsumer : BackgroundService
{
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQConsumer(
        ILogger<RabbitMQConsumer> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for RabbitMQ to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            InitializeRabbitMQ();
            
            if (_channel != null)
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    await HandleMessageAsync(ea, stoppingToken);
                };

                _channel.BasicConsume(
                    queue: "user_service_queue",
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("RabbitMQ consumer started. Listening for user events...");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start RabbitMQ consumer");
        }

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void InitializeRabbitMQ()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(
                exchange: "user_events",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare queue
            _channel.QueueDeclare(
                queue: "user_service_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind queue to exchange for UserCreated events from AuthService
            _channel.QueueBind(
                queue: "user_service_queue",
                exchange: "user_events",
                routingKey: "UserCreated");

            _logger.LogInformation("RabbitMQ consumer initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumer");
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

            _logger.LogInformation("Received message with routing key: {RoutingKey}", routingKey);

            if (routingKey == "UserCreated")
            {
                await HandleUserCreatedAsync(message, cancellationToken);
            }

            // Acknowledge the message
            _channel?.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            
            // Reject and requeue the message
            _channel?.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task HandleUserCreatedAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var userEvent = JsonSerializer.Deserialize<UserCreatedEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (userEvent == null)
            {
                _logger.LogWarning("Failed to deserialize UserCreated event");
                return;
            }

            _logger.LogInformation("Processing UserCreated event for UserId: {UserId}, Email: {Email}",
                userEvent.UserId, userEvent.Email);

            // Create user profile in UserService using scoped service
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<Application.Services.IUserService>();

            // Check if user already exists
            var existingUser = await userService.GetUserByIdAsync(userEvent.UserId, cancellationToken);
            if (existingUser != null)
            {
                _logger.LogInformation("User profile already exists for UserId: {UserId}", userEvent.UserId);
                return;
            }

            // Create user profile
            var createDto = new Application.DTOs.CreateUserDto
            {
                Email = userEvent.Email,
                Name = string.IsNullOrWhiteSpace(userEvent.Name) ? "New User" : userEvent.Name,
                Role = userEvent.Role ?? "Student"
            };

            await userService.CreateUserAsync(createDto, cancellationToken);
            
            _logger.LogInformation("User profile created successfully for UserId: {UserId}", userEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserCreated event");
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
    private class UserCreatedEvent
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Role { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

