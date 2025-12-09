using MediatR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Application.Commands.Books;
using SearchService.Application.Commands.Stock;
using SearchService.Application.Common.Models;
using System.Text;
using System.Text.Json;

namespace SearchService.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ consumer for book events - refactored to use MediatR
/// </summary>
public class BookEventConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookEventConsumer> _logger;
    private readonly IConfiguration _configuration;

    public BookEventConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<BookEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

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
            _channel.ExchangeDeclare(exchange: "book_events", type: ExchangeType.Direct, durable: true);

            // Declare queue
            var queueName = _channel.QueueDeclare().QueueName;

            // Bind to routing keys
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookCreated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookUpdated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookDeleted");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookStockUpdated");
            _channel.QueueBind(queue: queueName, exchange: "book_events", routingKey: "BookStockRemoved");

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
                await ProcessMessageAsync(routingKey, message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with routing key {RoutingKey}", routingKey);
            }
        };

        _channel.BasicConsume(queue: "", autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string routingKey, string message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        _logger.LogInformation("Processing {RoutingKey} event", routingKey);

        switch (routingKey)
        {
            case "BookCreated":
                await HandleBookCreatedAsync(mediator, message, cancellationToken);
                break;

            case "BookUpdated":
                await HandleBookUpdatedAsync(mediator, message, cancellationToken);
                break;

            case "BookDeleted":
                await HandleBookDeletedAsync(mediator, message, cancellationToken);
                break;

            case "BookStockUpdated":
                await HandleBookStockUpdatedAsync(mediator, message, cancellationToken);
                break;

            case "BookStockRemoved":
                await HandleBookStockRemovedAsync(mediator, message, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
                break;
        }
    }

    private async Task HandleBookCreatedAsync(IMediator mediator, string message, CancellationToken cancellationToken)
    {
        try
        {
            var bookEvent = JsonSerializer.Deserialize<BookEventDto>(message);
            if (bookEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookCreated event");
                return;
            }

            var command = new CreateBookCommand(
                bookEvent.ISBN,
                bookEvent.BookTitle,
                bookEvent.BookAuthor,
                bookEvent.YearOfPublication,
                bookEvent.Publisher,
                bookEvent.ImageUrlS,
                bookEvent.ImageUrlM,
                bookEvent.ImageUrlL,
                bookEvent.Genre ?? "",
                bookEvent.Language ?? "English",
                bookEvent.PageCount,
                bookEvent.Description ?? "",
                bookEvent.Rating,
                bookEvent.AvailabilityStatus ?? "Available",
                bookEvent.Edition ?? "",
                bookEvent.Format ?? "Paperback"
            );

            var result = await mediator.Send(command, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully created book with ISBN {ISBN}", bookEvent.ISBN);
            }
            else
            {
                _logger.LogError("Failed to create book with ISBN {ISBN}: {Error}", bookEvent.ISBN, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BookCreated event");
        }
    }

    private async Task HandleBookUpdatedAsync(IMediator mediator, string message, CancellationToken cancellationToken)
    {
        try
        {
            var bookEvent = JsonSerializer.Deserialize<BookEventDto>(message);
            if (bookEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookUpdated event");
                return;
            }

            var command = new UpdateBookCommand(
                bookEvent.ISBN,
                bookEvent.BookTitle,
                bookEvent.BookAuthor,
                bookEvent.YearOfPublication,
                bookEvent.Publisher,
                bookEvent.ImageUrlS,
                bookEvent.ImageUrlM,
                bookEvent.ImageUrlL,
                bookEvent.Genre ?? "",
                bookEvent.Language ?? "English",
                bookEvent.PageCount,
                bookEvent.Description ?? "",
                bookEvent.Rating,
                bookEvent.AvailabilityStatus ?? "Available",
                bookEvent.Edition ?? "",
                bookEvent.Format ?? "Paperback"
            );

            var result = await mediator.Send(command, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully updated book with ISBN {ISBN}", bookEvent.ISBN);
            }
            else
            {
                _logger.LogError("Failed to update book with ISBN {ISBN}: {Error}", bookEvent.ISBN, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BookUpdated event");
        }
    }

    private async Task HandleBookDeletedAsync(IMediator mediator, string message, CancellationToken cancellationToken)
    {
        try
        {
            var bookEvent = JsonSerializer.Deserialize<BookEventDto>(message);
            if (bookEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookDeleted event");
                return;
            }

            var command = new DeleteBookCommand(bookEvent.ISBN);
            var result = await mediator.Send(command, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully deleted book with ISBN {ISBN}", bookEvent.ISBN);
            }
            else
            {
                _logger.LogError("Failed to delete book with ISBN {ISBN}: {Error}", bookEvent.ISBN, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BookDeleted event");
        }
    }

    private async Task HandleBookStockUpdatedAsync(IMediator mediator, string message, CancellationToken cancellationToken)
    {
        try
        {
            var stockEvent = JsonSerializer.Deserialize<StockEventDto>(message);
            if (stockEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookStockUpdated event");
                return;
            }

            _logger.LogInformation("Processing BookStockUpdated event for ISBN {ISBN}. Using aggregated data from event.", stockEvent.BookISBN);

            // Log sellers information if available
            if (stockEvent.Sellers != null && stockEvent.Sellers.Any())
            {
                _logger.LogInformation("Event contains {Count} seller entries for ISBN {ISBN}", stockEvent.Sellers.Count, stockEvent.BookISBN);
            }
            else
            {
                _logger.LogWarning("Event does not contain seller entries for ISBN {ISBN}. This may indicate backward compatibility issue or missing data.", stockEvent.BookISBN);
            }

            // Use aggregated data directly from event (no HTTP call to WarehouseService)
            var command = new UpdateBookStockCommand(
                stockEvent.BookISBN,
                stockEvent.TotalStock,
                stockEvent.AvailableSellers,
                stockEvent.MinPrice,
                stockEvent.Sellers // Pass sellers data to command
            );

            _logger.LogInformation("Using aggregated stock from event for ISBN {ISBN}: TotalStock={TotalStock}, AvailableSellers={AvailableSellers}, MinPrice={MinPrice}, SellersCount={SellersCount}",
                stockEvent.BookISBN, stockEvent.TotalStock, stockEvent.AvailableSellers, stockEvent.MinPrice, stockEvent.Sellers?.Count ?? 0);

            var result = await mediator.Send(command, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully updated stock for book ISBN {ISBN}", stockEvent.BookISBN);
            }
            else
            {
                _logger.LogError("Failed to update stock for book ISBN {ISBN}: {Error}", stockEvent.BookISBN, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BookStockUpdated event");
        }
    }

    private async Task HandleBookStockRemovedAsync(IMediator mediator, string message, CancellationToken cancellationToken)
    {
        try
        {
            var removedEvent = JsonSerializer.Deserialize<BookStockRemovedEventDto>(message);
            if (removedEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookStockRemoved event");
                return;
            }

            _logger.LogInformation("Processing BookStockRemoved event for ISBN: {ISBN}, SellerId: {SellerId}", 
                removedEvent.BookISBN, removedEvent.SellerId);

            // Remove the specific seller from sellers data in Redis
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.ICacheService>();
            
            var sellersKey = $"sellers:{removedEvent.BookISBN}";
            var sellersJson = await cacheService.GetAsync<string>(sellersKey, cancellationToken);

            if (!string.IsNullOrEmpty(sellersJson))
            {
                try
                {
                    var sellers = JsonSerializer.Deserialize<List<SellerInfoDto>>(sellersJson) 
                        ?? new List<SellerInfoDto>();

                    // Remove seller with matching SellerId
                    var initialCount = sellers.Count;
                    sellers.RemoveAll(s => s.SellerId == removedEvent.SellerId);
                    var removedCount = initialCount - sellers.Count;

                    if (removedCount > 0)
                    {
                        if (sellers.Any())
                        {
                            // Update sellers list without the removed seller
                            var updatedSellersJson = JsonSerializer.Serialize(sellers);
                            await cacheService.SetAsync(sellersKey, updatedSellersJson, cancellationToken: cancellationToken);
                            _logger.LogInformation("Removed seller {SellerId} from sellers data for ISBN: {ISBN}. Remaining sellers: {Count}", 
                                removedEvent.SellerId, removedEvent.BookISBN, sellers.Count);
                        }
                        else
                        {
                            // If no sellers left, remove the key
                            await cacheService.RemoveAsync(sellersKey, cancellationToken);
                            _logger.LogInformation("Removed last seller {SellerId} from ISBN: {ISBN}. Removed sellers key.", 
                                removedEvent.SellerId, removedEvent.BookISBN);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Seller {SellerId} not found in sellers data for ISBN: {ISBN}. Seller may have already been removed or never existed.", 
                            removedEvent.SellerId, removedEvent.BookISBN);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize sellers data for ISBN: {ISBN}. Removing corrupted key.", removedEvent.BookISBN);
                    // Remove corrupted data
                    await cacheService.RemoveAsync(sellersKey, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("No sellers data found for ISBN: {ISBN}. Nothing to remove.", removedEvent.BookISBN);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BookStockRemoved event");
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }

    // DTOs for deserialization
    private class BookEventDto
    {
        public string ISBN { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public string BookAuthor { get; set; } = string.Empty;
        public int YearOfPublication { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public string? ImageUrlS { get; set; }
        public string? ImageUrlM { get; set; }
        public string? ImageUrlL { get; set; }
        public string? Genre { get; set; }
        public string? Language { get; set; }
        public int PageCount { get; set; }
        public string? Description { get; set; }
        public double Rating { get; set; }
        public string? AvailabilityStatus { get; set; }
        public string? Edition { get; set; }
        public string? Format { get; set; }
    }

    private class StockEventDto
    {
        public string BookISBN { get; set; } = string.Empty;
        public int TotalStock { get; set; }
        public int AvailableSellers { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AveragePrice { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<SellerInfoDto>? Sellers { get; set; } // Individual seller entries
    }

    private class BookStockRemovedEventDto
    {
        public int Id { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
    }
}

