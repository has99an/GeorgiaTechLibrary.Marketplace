using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AuthService.Models;
using AuthService.Repositories;

namespace AuthService.Services;

public class RabbitMQConsumer : BackgroundService, IMessageConsumer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ILogger<RabbitMQConsumer> _logger;

    public RabbitMQConsumer(
        IConfiguration configuration,
        IAuthUserRepository authUserRepository,
        ILogger<RabbitMQConsumer> logger)
    {
        _authUserRepository = authUserRepository;
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

            // Declare queue
            var queueName = _channel.QueueDeclare().QueueName;

            // Bind to routing key for UserCreated
            _channel.QueueBind(queue: queueName, exchange: "user_events", routingKey: "UserCreated");

            _logger.LogInformation("RabbitMQ Consumer connected and bound to UserCreated events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
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
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            try
            {
                await ProcessMessageAsync(routingKey, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with routing key {RoutingKey}", routingKey);
            }
        };

        _channel.BasicConsume(queue: "", autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message)
    {
        switch (routingKey)
        {
            case "UserCreated":
                var userEvent = JsonSerializer.Deserialize<UserEvent>(message);
                if (userEvent != null)
                {
                    await HandleUserCreatedAsync(userEvent);
                }
                break;
        }
    }

    private async Task HandleUserCreatedAsync(UserEvent userEvent)
    {
        // Check if AuthUser already exists
        var existingAuthUser = await _authUserRepository.GetAuthUserByIdAsync(userEvent.UserId);
        if (existingAuthUser == null)
        {
            // Create AuthUser with default password or something
            // For now, create with empty password hash, assuming password will be set later
            var authUser = new AuthUser
            {
                UserId = userEvent.UserId,
                Email = userEvent.Email,
                PasswordHash = "", // Will be set by register
                CreatedDate = DateTime.UtcNow
            };

            await _authUserRepository.AddAuthUserAsync(authUser);
            _logger.LogInformation("Created AuthUser for UserId {UserId}", userEvent.UserId);
        }
        else
        {
            _logger.LogInformation("AuthUser already exists for UserId {UserId}", userEvent.UserId);
        }
    }

    public void StartConsuming()
    {
        // Consumer starts automatically via BackgroundService
    }

    public void StopConsuming()
    {
        _channel?.Close();
        _connection?.Close();
    }

    public override void Dispose()
    {
        StopConsuming();
        base.Dispose();
    }
}
