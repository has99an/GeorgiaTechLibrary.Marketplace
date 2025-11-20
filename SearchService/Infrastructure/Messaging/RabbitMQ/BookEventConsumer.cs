using MediatR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Application.Commands.Books;
using SearchService.Application.Commands.Stock;
using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BookEventConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<BookEventConsumer> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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

            _logger.LogInformation("Processing BookStockUpdated event for ISBN {ISBN}. Aggregating all warehouse items.", stockEvent.BookISBN);

            // Aggregate all warehouse items for this ISBN from WarehouseService
            var aggregatedStock = await AggregateWarehouseItemsAsync(stockEvent.BookISBN, cancellationToken);

            var command = new UpdateBookStockCommand(
                stockEvent.BookISBN,
                aggregatedStock.TotalStock,
                aggregatedStock.AvailableSellers,
                aggregatedStock.MinPrice
            );

            _logger.LogInformation("Aggregated stock for ISBN {ISBN}: TotalStock={TotalStock}, AvailableSellers={AvailableSellers}, MinPrice={MinPrice}",
                stockEvent.BookISBN, aggregatedStock.TotalStock, aggregatedStock.AvailableSellers, aggregatedStock.MinPrice);

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

    private async Task<(int TotalStock, int AvailableSellers, decimal MinPrice)> AggregateWarehouseItemsAsync(
        string bookIsbn, 
        CancellationToken cancellationToken)
    {
        try
        {
            var warehouseServiceUrl = _configuration["WarehouseService:BaseUrl"] 
                ?? _configuration["Services:WarehouseService"] 
                ?? "http://warehouseservice:8080";
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Try the endpoint: /api/warehouse/items/{bookIsbn}
            var endpoint = $"{warehouseServiceUrl}/api/warehouse/items/{Uri.EscapeDataString(bookIsbn)}";
            _logger.LogDebug("Calling WarehouseService endpoint: {Endpoint}", endpoint);

            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WarehouseService returned {StatusCode} for ISBN {ISBN}. Assuming no stock.", 
                    response.StatusCode, bookIsbn);
                return (0, 0, 0);
            }

            var warehouseItems = await response.Content.ReadFromJsonAsync<List<WarehouseItemDto>>(cancellationToken: cancellationToken);
            
            if (warehouseItems == null || !warehouseItems.Any())
            {
                _logger.LogDebug("No warehouse items found for ISBN {ISBN}", bookIsbn);
                return (0, 0, 0);
            }

            // Filter items with quantity > 0
            var availableItems = warehouseItems.Where(item => item.Quantity > 0).ToList();

            if (!availableItems.Any())
            {
                _logger.LogDebug("No available warehouse items (quantity > 0) for ISBN {ISBN}", bookIsbn);
                return (0, 0, 0);
            }

            // Calculate aggregates
            var totalStock = availableItems.Sum(item => item.Quantity);
            var availableSellers = availableItems.Select(item => item.SellerId).Distinct().Count();
            var minPrice = availableItems.Min(item => item.Price);

            _logger.LogDebug("Aggregated for ISBN {ISBN}: {TotalStock} total stock from {AvailableSellers} sellers, min price {MinPrice}",
                bookIsbn, totalStock, availableSellers, minPrice);

            return (totalStock, availableSellers, minPrice);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to call WarehouseService for ISBN {ISBN}. Using event data as fallback.", bookIsbn);
            // Fallback: return 0 to indicate no stock (safer than using potentially stale event data)
            return (0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating warehouse items for ISBN {ISBN}", bookIsbn);
            return (0, 0, 0);
        }
    }

    private class WarehouseItemDto
    {
        public int Id { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; } = string.Empty;
        public bool IsNew { get; set; }
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
        public int Id { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Condition { get; set; } = string.Empty;
    }
}

