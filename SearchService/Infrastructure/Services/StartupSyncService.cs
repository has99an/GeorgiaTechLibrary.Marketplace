using SearchService.Application.Common.Interfaces;
using SearchService.Infrastructure.Common;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Services;

/// <summary>
/// Service that runs on startup to check and sync data if needed
/// </summary>
public class StartupSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupSyncService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;

    public StartupSyncService(
        IServiceProvider serviceProvider,
        ILogger<StartupSyncService> logger,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _redis = redis;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for services to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            _logger.LogInformation("StartupSyncService: Checking Redis state...");

            var database = _redis.GetDatabase();
            var titleKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
            var priceKey = RedisKeyBuilder.BuildAvailableBooksKey("price");

            var titleSize = await database.SortedSetLengthAsync(titleKey);
            var priceSize = await database.SortedSetLengthAsync(priceKey);

            _logger.LogInformation("StartupSyncService: Sorted sets - Title: {TitleSize}, Price: {PriceSize}", titleSize, priceSize);

            // Check if sorted sets are empty
            if (titleSize == 0 && priceSize == 0)
            {
                _logger.LogWarning("StartupSyncService: Sorted sets are empty. Attempting to rebuild from existing books...");

                using var scope = _serviceProvider.CreateScope();
                var bookRepository = scope.ServiceProvider.GetRequiredService<IBookRepository>();

                var rebuiltCount = await bookRepository.RebuildSortedSetsAsync(stoppingToken);

                if (rebuiltCount > 0)
                {
                    _logger.LogInformation("StartupSyncService: Successfully rebuilt sorted sets with {Count} available books", rebuiltCount);
                }
                else
                {
                    _logger.LogWarning("StartupSyncService: No available books found after rebuild. Books may need warehouse items to be synced.");

                    // Optionally trigger sync from other services
                    var shouldTriggerSync = _configuration.GetValue<bool>("Startup:TriggerSyncIfEmpty", false);
                    if (shouldTriggerSync)
                    {
                        _logger.LogInformation("StartupSyncService: Triggering sync from BookService and WarehouseService...");
                        await TriggerSyncFromServicesAsync(stoppingToken);
                    }
                }
            }
            else
            {
                _logger.LogInformation("StartupSyncService: Sorted sets are populated. No action needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupSyncService: Error during startup sync check");
        }
    }

    private async Task TriggerSyncFromServicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Sync can take time

            var bookServiceUrl = _configuration["BookService:BaseUrl"] 
                ?? _configuration["Services:BookService"] 
                ?? "http://bookservice:8080";
            var warehouseServiceUrl = _configuration["WarehouseService:BaseUrl"] 
                ?? _configuration["Services:WarehouseService"] 
                ?? "http://warehouseservice:8080";

            // Trigger BookService sync
            try
            {
                var bookSyncUrl = $"{bookServiceUrl}/api/books/sync-events";
                _logger.LogInformation("StartupSyncService: Triggering BookService sync at {Url}", bookSyncUrl);
                var bookResponse = await httpClient.PostAsync(bookSyncUrl, null, cancellationToken);
                if (bookResponse.IsSuccessStatusCode)
                {
                    var bookCount = await bookResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("StartupSyncService: BookService sync completed. Synced {Count} books", bookCount);
                }
                else
                {
                    _logger.LogWarning("StartupSyncService: BookService sync returned {StatusCode}", bookResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StartupSyncService: Failed to trigger BookService sync");
            }

            // Wait a bit for books to be processed
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

            // Trigger WarehouseService sync
            try
            {
                var warehouseSyncUrl = $"{warehouseServiceUrl}/api/warehouse/sync-events";
                _logger.LogInformation("StartupSyncService: Triggering WarehouseService sync at {Url}", warehouseSyncUrl);
                var warehouseResponse = await httpClient.PostAsync(warehouseSyncUrl, null, cancellationToken);
                if (warehouseResponse.IsSuccessStatusCode)
                {
                    var warehouseCount = await warehouseResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("StartupSyncService: WarehouseService sync completed. Synced {Count} items", warehouseCount);
                }
                else
                {
                    _logger.LogWarning("StartupSyncService: WarehouseService sync returned {StatusCode}", warehouseResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StartupSyncService: Failed to trigger WarehouseService sync");
            }

            // Wait for events to be processed, then rebuild sorted sets
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            using var scope = _serviceProvider.CreateScope();
            var bookRepository = scope.ServiceProvider.GetRequiredService<IBookRepository>();
            var rebuiltCount = await bookRepository.RebuildSortedSetsAsync(cancellationToken);
            _logger.LogInformation("StartupSyncService: Rebuilt sorted sets after sync with {Count} available books", rebuiltCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupSyncService: Error triggering sync from services");
        }
    }
}

