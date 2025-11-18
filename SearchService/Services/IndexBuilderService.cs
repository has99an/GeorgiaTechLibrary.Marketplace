using SearchService.Repositories;
using StackExchange.Redis;
using System.Text.Json;
using SearchService.Models;

namespace SearchService.Services;

public class IndexBuilderService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexBuilderService> _logger;
    private readonly IConfiguration _configuration;

    public IndexBuilderService(
        IServiceProvider serviceProvider, 
        ILogger<IndexBuilderService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== INDEX BUILDER: Starting index build at startup ===");
        
        try
        {
            var redisConnectionString = _configuration["Redis:ConnectionString"] ?? "localhost:6379";
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.SyncTimeout = 30000; // 30 seconds for index building
            configOptions.AsyncTimeout = 30000;
            
            using var redis = await ConnectionMultiplexer.ConnectAsync(configOptions);
            var database = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints().First());

            // Check if indexes already exist
            var titleIndexExists = await database.KeyExistsAsync("available:books:by:title");
            var priceIndexExists = await database.KeyExistsAsync("available:books:by:price");

            if (titleIndexExists && priceIndexExists)
            {
                _logger.LogInformation("Indexes already exist, skipping build");
                return;
            }

            _logger.LogInformation("Building indexes from scratch...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Build both indexes in parallel
            await Task.WhenAll(
                BuildTitleIndexAsync(database, server, cancellationToken),
                BuildPriceIndexAsync(database, server, cancellationToken)
            );

            stopwatch.Stop();
            _logger.LogInformation("=== INDEX BUILDER: Completed in {Elapsed}ms ===", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building indexes at startup");
        }
    }

    private async Task BuildTitleIndexAsync(IDatabase database, IServer server, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Building title-sorted index...");
            var setKey = "available:books:by:title";
            var count = 0;
            var entries = new List<SortedSetEntry>();

            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 1000))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var bookJson = await database.StringGetAsync(key);
                if (!bookJson.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(bookJson!);
                    if (book != null && book.AvailableSellers > 0)
                    {
                        var score = GetTitleScore(book.Title);
                        entries.Add(new SortedSetEntry(book.Isbn, score));
                        count++;

                        // Add in batches of 1000
                        if (entries.Count >= 1000)
                        {
                            await database.SortedSetAddAsync(setKey, entries.ToArray());
                            entries.Clear();
                            
                            if (count % 10000 == 0)
                            {
                                _logger.LogInformation("Title index: {Count} books indexed", count);
                            }
                        }
                    }
                }
            }

            // Add remaining
            if (entries.Any())
            {
                await database.SortedSetAddAsync(setKey, entries.ToArray());
            }

            // Set to never expire (we'll update it when books change)
            await database.KeyPersistAsync(setKey);
            
            _logger.LogInformation("Title index complete: {Count} books", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building title index");
        }
    }

    private async Task BuildPriceIndexAsync(IDatabase database, IServer server, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Building price-sorted index...");
            var setKey = "available:books:by:price";
            var count = 0;
            var entries = new List<SortedSetEntry>();

            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 1000))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var bookJson = await database.StringGetAsync(key);
                if (!bookJson.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(bookJson!);
                    if (book != null && book.AvailableSellers > 0 && book.MinPrice > 0)
                    {
                        entries.Add(new SortedSetEntry(book.Isbn, (double)book.MinPrice));
                        count++;

                        if (entries.Count >= 1000)
                        {
                            await database.SortedSetAddAsync(setKey, entries.ToArray());
                            entries.Clear();
                            
                            if (count % 10000 == 0)
                            {
                                _logger.LogInformation("Price index: {Count} books indexed", count);
                            }
                        }
                    }
                }
            }

            if (entries.Any())
            {
                await database.SortedSetAddAsync(setKey, entries.ToArray());
            }

            await database.KeyPersistAsync(setKey);
            
            _logger.LogInformation("Price index complete: {Count} books", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building price index");
        }
    }

    private double GetTitleScore(string title)
    {
        // Create score from first 8 characters for better sorting
        var titleLower = title.ToLowerInvariant().PadRight(8, 'z');
        var score = 0.0;
        
        for (int i = 0; i < Math.Min(8, titleLower.Length); i++)
        {
            score = score * 128 + (int)titleLower[i];
        }
        
        return score;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

