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
        // Wait longer initially for RabbitMQ to be fully ready
        // Even though healthcheck passes, RabbitMQ might need more time to accept connections
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        // Retry logic for RabbitMQ connection
        const int maxRetries = 15;
        const int initialDelaySeconds = 5;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ (attempt {Attempt}/{MaxRetries})...", 
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
                        queue: "user_service_queue",
                        autoAck: false,
                        consumer: consumer);

                    _logger.LogInformation("RabbitMQ consumer started. Listening for user events...");
                    
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
                    // Exponential backoff: 5s, 10s, 20s, 40s, etc. (max 60s)
                    var delaySeconds = Math.Min(initialDelaySeconds * (int)Math.Pow(2, attempt - 1), 60);
                    _logger.LogInformation("Retrying in {Delay} seconds...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Failed to connect to RabbitMQ after {MaxRetries} attempts. Consumer will not start.", 
                        maxRetries);
                    // Keep service running even if RabbitMQ connection fails
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

            _logger.LogInformation("Received message with routing key: {RoutingKey}, Message: {Message}", routingKey, message);

            if (routingKey == "UserCreated")
            {
                await HandleUserCreatedAsync(message, cancellationToken);
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
            _logger.LogError(ex, "Error handling message. RoutingKey: {RoutingKey}, Error: {Error}", ea.RoutingKey, ex.Message);
            
            // Reject and requeue the message
            _channel?.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task HandleUserCreatedAsync(string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== USERCREATED EVENT RECEIVED IN USERSERVICE ===");
        _logger.LogInformation("Raw message received. Length: {Length} characters", message?.Length ?? 0);
        _logger.LogInformation("Raw message content: {Message}", message);
        
        UserCreatedEvent? userEvent = null;
        try
        {
            _logger.LogInformation("Step 1: Deserializing UserCreated event from JSON...");
            if (string.IsNullOrEmpty(message))
            {
                _logger.LogWarning("Step 1: Message is null or empty, cannot deserialize");
                return;
            }
            userEvent = JsonSerializer.Deserialize<UserCreatedEvent>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (userEvent == null)
            {
                _logger.LogError("Step 1: FAILED - Failed to deserialize UserCreated event. Message: {Message}", message);
                return;
            }
            _logger.LogInformation("Step 1: Deserialization successful - UserId: {UserId}, Email: {Email}, Name: {Name}, Role: {Role}, CreatedDate: {CreatedDate}",
                userEvent.UserId, userEvent.Email, userEvent.Name, userEvent.Role, userEvent.CreatedDate);

            _logger.LogInformation("=== PROCESSING USERCREATED EVENT ===");
            _logger.LogInformation("Event details - UserId: {UserId}, Email: {Email}, Name: {Name}, Role: {Role}",
                userEvent.UserId, userEvent.Email, userEvent.Name, userEvent.Role);

            // Create user profile in UserService using scoped service
            _logger.LogInformation("Step 2: Creating service scope...");
            using var scope = _serviceProvider.CreateScope();
            _logger.LogInformation("Step 2: Service scope created");
            
            _logger.LogInformation("Step 3: Resolving IUserService from DI container...");
            var userService = scope.ServiceProvider.GetRequiredService<Application.Services.IUserService>();
            _logger.LogInformation("Step 3: IUserService resolved successfully");

            // Check if user already exists
            _logger.LogInformation("Step 4: Checking if user already exists in UserService...");
            _logger.LogInformation("Step 4: Calling userService.GetUserByIdAsync with UserId: {UserId}", userEvent.UserId);
            var existingUser = await userService.GetUserByIdAsync(userEvent.UserId, cancellationToken);
            if (existingUser != null)
            {
                _logger.LogInformation("Step 4: User profile already exists for UserId: {UserId}, Email: {Email}", 
                    userEvent.UserId, existingUser.Email);
                _logger.LogInformation("=== SKIPPING USER CREATION - USER ALREADY EXISTS ===");
                return;
            }
            _logger.LogInformation("Step 4: User does not exist, proceeding with creation...");

            // Create user profile with the UserId from AuthService
            _logger.LogInformation("Step 5: Creating CreateUserDto...");
            var createDto = new Application.DTOs.CreateUserDto
            {
                Email = userEvent.Email,
                Name = string.IsNullOrWhiteSpace(userEvent.Name) ? "New User" : userEvent.Name,
                Role = userEvent.Role ?? "Student"
            };
            _logger.LogInformation("Step 5: CreateUserDto created - Email: {Email}, Name: {Name}, Role: {Role}",
                createDto.Email, createDto.Name, createDto.Role);

            _logger.LogInformation("Step 6: Calling userService.CreateUserWithIdAsync...");
            _logger.LogInformation("Step 6: Parameters - UserId: {UserId}, Email: {Email}, Name: {Name}, Role: {Role}",
                userEvent.UserId, createDto.Email, createDto.Name, createDto.Role);
            await userService.CreateUserWithIdAsync(userEvent.UserId, createDto, cancellationToken);
            _logger.LogInformation("Step 6: userService.CreateUserWithIdAsync completed successfully");
            
            _logger.LogInformation("=== USER PROFILE CREATED SUCCESSFULLY ===");
            _logger.LogInformation("Final result - UserId: {UserId}, Email: {Email}", 
                userEvent.UserId, userEvent.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== ERROR PROCESSING USERCREATED EVENT ===");
            _logger.LogError(ex, "Exception details - UserId: {UserId}, Email: {Email}, ExceptionType: {ExceptionType}, Message: {Message}",
                userEvent?.UserId ?? Guid.Empty, userEvent?.Email ?? "unknown", ex.GetType().Name, ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            _logger.LogError(ex, "Inner exception: {InnerException}", ex.InnerException?.Message);
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

