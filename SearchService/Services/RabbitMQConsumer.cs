using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Models;
using SearchService.Repositories;

namespace SearchService.Services;

public class RabbitMQConsumer : BackgroundService, IMessageConsumer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumer> _logger;

    public RabbitMQConsumer(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<RabbitMQConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct);

            // Declare queue
            var queueName = _channel.QueueDeclare().QueueName;

            // Bind to routing keys
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookCreated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookUpdated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookDeleted");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookStockUpdated");

            _logger.LogInformation("RabbitMQ Consumer connected and bound to events");
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
        using var scope = _serviceProvider.CreateScope();
        var searchRepository = scope.ServiceProvider.GetRequiredService<ISearchRepository>();

        switch (routingKey)
        {
            case "BookCreated":
            case "BookUpdated":
                var bookEvent = JsonSerializer.Deserialize<BookEvent>(message);
                if (bookEvent != null)
                {
                    await HandleBookEventAsync(searchRepository, bookEvent, routingKey == "BookCreated");
                }
                break;

            case "BookDeleted":
                var deleteEvent = JsonSerializer.Deserialize<BookEvent>(message);
                if (deleteEvent != null)
                {
                    await searchRepository.DeleteBookAsync(deleteEvent.ISBN);
                }
                break;

            case "BookStockUpdated":
                var stockEvent = JsonSerializer.Deserialize<WarehouseStockEvent>(message);
                if (stockEvent != null)
                {
                    await HandleStockUpdateAsync(searchRepository, stockEvent);
                }
                break;
        }
    }

    private async Task HandleBookEventAsync(ISearchRepository repository, BookEvent bookEvent, bool isCreated)
    {
        var bookSearchModel = new BookSearchModel
        {
            Isbn = bookEvent.ISBN,
            Title = bookEvent.BookTitle,
            Author = bookEvent.BookAuthor,
            YearOfPublication = bookEvent.YearOfPublication,
            Publisher = bookEvent.Publisher,
            ImageUrlS = bookEvent.ImageUrlS,
            ImageUrlM = bookEvent.ImageUrlM,
            ImageUrlL = bookEvent.ImageUrlL,
            TotalStock = 0, // Will be updated by stock events
            AvailableSellers = 0,
            MinPrice = 0
        };

        await repository.AddOrUpdateBookAsync(bookSearchModel);
        _logger.LogInformation("{Action} book with ISBN {Isbn}", isCreated ? "Created" : "Updated", bookEvent.ISBN);
    }

    private async Task HandleStockUpdateAsync(ISearchRepository repository, WarehouseStockEvent stockEvent)
    {
        // This is a simplified version. In a real implementation, you would need to aggregate
        // all warehouse items for this ISBN from the WarehouseService or maintain local cache.
        // For now, we'll just update with the current item's data as an example.

        // In production, you would call WarehouseService API or maintain a local cache
        // of all warehouse items to properly aggregate total stock, available sellers, and min price.

        var totalStock = stockEvent.Quantity; // This should be sum of all quantities
        var availableSellers = stockEvent.Quantity > 0 ? 1 : 0; // This should be count of unique sellers with stock
        var minPrice = stockEvent.Quantity > 0 ? stockEvent.Price : 0; // This should be min price among available items

        await repository.UpdateBookStockAsync(stockEvent.BookISBN, totalStock, availableSellers, minPrice);
        _logger.LogInformation("Updated stock for book ISBN {Isbn}", stockEvent.BookISBN);
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
