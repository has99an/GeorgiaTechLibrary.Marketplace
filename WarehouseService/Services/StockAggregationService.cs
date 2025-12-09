using WarehouseService.Repositories;

namespace WarehouseService.Services;

/// <summary>
/// Service for aggregating stock data and publishing aggregated events
/// </summary>
public class StockAggregationService
{
    private readonly IWarehouseItemRepository _warehouseRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<StockAggregationService> _logger;

    public StockAggregationService(
        IWarehouseItemRepository warehouseRepository,
        IMessageProducer messageProducer,
        ILogger<StockAggregationService> logger)
    {
        _warehouseRepository = warehouseRepository;
        _messageProducer = messageProducer;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates stock data for a BookISBN and publishes BookStockUpdated event with aggregated data
    /// </summary>
    public async Task PublishAggregatedStockEventAsync(string bookISBN)
    {
        try
        {
            _logger.LogInformation("Aggregating stock data for BookISBN: {BookISBN}", bookISBN);

            // Get all warehouse items for this BookISBN
            var allItems = await _warehouseRepository.GetWarehouseItemsByBookIsbnAsync(bookISBN);
            var itemsList = allItems.ToList();

            if (!itemsList.Any())
            {
                _logger.LogWarning("No warehouse items found for BookISBN: {BookISBN}", bookISBN);
                // Publish event with zero stock
                var zeroStockEvent = new
                {
                    BookISBN = bookISBN,
                    TotalStock = 0,
                    AvailableSellers = 0,
                    MinPrice = 0m,
                    MaxPrice = 0m,
                    AveragePrice = 0m,
                    UpdatedAt = DateTime.UtcNow,
                    Sellers = new List<object>() // Empty sellers array
                };
                _messageProducer.SendMessage(zeroStockEvent, "BookStockUpdated");
                return;
            }

            // Filter items with quantity > 0
            var availableItems = itemsList.Where(item => item.Quantity > 0).ToList();

            if (!availableItems.Any())
            {
                _logger.LogInformation("No available stock (quantity > 0) for BookISBN: {BookISBN}", bookISBN);
                // Publish event with zero stock
                var zeroStockEvent = new
                {
                    BookISBN = bookISBN,
                    TotalStock = 0,
                    AvailableSellers = 0,
                    MinPrice = 0m,
                    MaxPrice = 0m,
                    AveragePrice = 0m,
                    UpdatedAt = DateTime.UtcNow,
                    Sellers = new List<object>() // Empty sellers array
                };
                _messageProducer.SendMessage(zeroStockEvent, "BookStockUpdated");
                return;
            }

            // Calculate aggregates
            var totalStock = availableItems.Sum(item => item.Quantity);
            var availableSellers = availableItems.Select(item => item.SellerId).Distinct().Count();
            var prices = availableItems.Select(item => item.Price).ToList();
            var minPrice = prices.Min();
            var maxPrice = prices.Max();
            var averagePrice = prices.Average();

            _logger.LogInformation("Aggregated stock for BookISBN {BookISBN}: TotalStock={TotalStock}, AvailableSellers={AvailableSellers}, MinPrice={MinPrice}, MaxPrice={MaxPrice}, AveragePrice={AveragePrice}",
                bookISBN, totalStock, availableSellers, minPrice, maxPrice, averagePrice);

            // Create individual seller entries
            var sellers = availableItems.Select(item => new
            {
                SellerId = item.SellerId,
                Price = item.Price,
                Quantity = item.Quantity,
                Condition = item.IsNew ? "New" : "Used", // Map IsNew to Condition
                Location = item.Location,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            _logger.LogInformation("Created {Count} seller entries for BookISBN {BookISBN}", sellers.Count, bookISBN);

            // Publish aggregated event with individual seller entries
            var aggregatedEvent = new
            {
                BookISBN = bookISBN,
                TotalStock = totalStock,
                AvailableSellers = availableSellers,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                AveragePrice = averagePrice,
                UpdatedAt = DateTime.UtcNow,
                Sellers = sellers // Individual seller entries
            };

            _messageProducer.SendMessage(aggregatedEvent, "BookStockUpdated");
            _logger.LogInformation("Aggregated BookStockUpdated event published for BookISBN: {BookISBN} with {Count} sellers", bookISBN, sellers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating and publishing stock data for BookISBN: {BookISBN}", bookISBN);
            // Don't throw - allow operation to continue
        }
    }
}

