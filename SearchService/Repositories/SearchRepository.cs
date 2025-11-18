using System.Text.Json;
using System.Text.RegularExpressions;
using SearchService.Models;
using StackExchange.Redis;

namespace SearchService.Repositories;

public class SearchRepository : ISearchRepository, IDisposable
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SearchRepository> _logger;
    private static readonly Regex _wordRegex = new Regex(@"\w+", RegexOptions.Compiled);

    public SearchRepository(IConfiguration configuration, ILogger<SearchRepository> logger)
    {
        _logger = logger;

        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        
        // Configure Redis with increased timeout and optimized settings
        var configOptions = ConfigurationOptions.Parse(redisConnectionString);
        configOptions.SyncTimeout = 10000; // 10 seconds
        configOptions.AsyncTimeout = 10000; // 10 seconds
        configOptions.ConnectTimeout = 10000; // 10 seconds
        configOptions.AbortOnConnectFail = false;
        configOptions.KeepAlive = 60;
        
        _redis = ConnectionMultiplexer.Connect(configOptions);
        _database = _redis.GetDatabase();

        _logger.LogInformation("Connected to Redis with optimized settings");
    }

    private IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Empty<string>();

        return _wordRegex.Matches(text.ToLowerInvariant())
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct();
    }

    private async Task UpdateIndexAsync(string isbn, string title, string author)
    {
        var words = Tokenize(title).Concat(Tokenize(author)).Distinct();

        foreach (var word in words)
        {
            var indexKey = $"index:{word}";
            await _database.SetAddAsync(indexKey, isbn);
        }
    }

    private async Task RemoveFromIndexAsync(string isbn, string title, string author)
    {
        var words = Tokenize(title).Concat(Tokenize(author)).Distinct();

        foreach (var word in words)
        {
            var indexKey = $"index:{word}";
            await _database.SetRemoveAsync(indexKey, isbn);
        }
    }

    public async Task<BookSearchModel?> GetBookByIsbnAsync(string isbn)
    {
        try
        {
            var key = $"book:{isbn}";
            var value = await _database.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            var book = JsonSerializer.Deserialize<BookSearchModel>(value!);
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving book with ISBN {Isbn}", isbn);
            return null;
        }
    }

    public async Task<IEnumerable<BookSearchModel>> SearchBooksAsync(string query)
    {
        try
        {
            var queryWords = Tokenize(query).ToArray();
            if (!queryWords.Any())
                return Enumerable.Empty<BookSearchModel>();

            RedisValue[] isbnValues;

            if (queryWords.Length == 1)
            {
                // Single word search
                var indexKey = $"index:{queryWords[0]}";
                isbnValues = await _database.SetMembersAsync(indexKey);
            }
            else
            {
                // Multi-word search - intersect all word sets
                var indexKeys = queryWords.Select(w => (RedisKey)$"index:{w}").ToArray();
                isbnValues = await _database.SetCombineAsync(SetOperation.Intersect, indexKeys);
            }

            if (!isbnValues.Any())
                return Enumerable.Empty<BookSearchModel>();

            // Fetch books for the ISBNs
            var books = new List<BookSearchModel>();
            var batch = _database.CreateBatch();

            var tasks = isbnValues.Select(isbn =>
                batch.StringGetAsync($"book:{isbn}")).ToArray();

            batch.Execute();

            foreach (var task in tasks)
            {
                var value = await task;
                if (!value.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(value!);
                    if (book != null)
                    {
                        books.Add(book);
                    }
                }
            }

            return books.OrderBy(b => b.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching books with query {Query}", query);
            return Enumerable.Empty<BookSearchModel>();
        }
    }

    public async Task AddOrUpdateBookAsync(BookSearchModel book)
    {
        try
        {
            // Remove old index entries if updating
            var existingBook = await GetBookByIsbnAsync(book.Isbn);
            if (existingBook != null)
            {
                await RemoveFromIndexAsync(book.Isbn, existingBook.Title, existingBook.Author);
                
                // Remove from sorted sets
                await _database.SortedSetRemoveAsync("available:books:by:title", book.Isbn);
                await _database.SortedSetRemoveAsync("available:books:by:price", book.Isbn);
            }

            // Store the book
            var key = $"book:{book.Isbn}";
            var json = JsonSerializer.Serialize(book);
            await _database.StringSetAsync(key, json);

            // Add new index entries
            await UpdateIndexAsync(book.Isbn, book.Title, book.Author);

            // Update sorted sets if book is available
            if (book.AvailableSellers > 0)
            {
                var titleScore = GetTitleScore(book.Title);
                await _database.SortedSetAddAsync("available:books:by:title", book.Isbn, titleScore);
                
                if (book.MinPrice > 0)
                {
                    await _database.SortedSetAddAsync("available:books:by:price", book.Isbn, (double)book.MinPrice);
                }
            }

            _logger.LogInformation("Added/Updated book with ISBN {Isbn}", book.Isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating book with ISBN {Isbn}", book.Isbn);
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

    public async Task DeleteBookAsync(string isbn)
    {
        try
        {
            // Get the book to remove from index
            var existingBook = await GetBookByIsbnAsync(isbn);
            if (existingBook != null)
            {
                await RemoveFromIndexAsync(isbn, existingBook.Title, existingBook.Author);
            }

            // Remove from sorted sets
            await _database.SortedSetRemoveAsync("available:books:by:title", isbn);
            await _database.SortedSetRemoveAsync("available:books:by:price", isbn);

            // Delete the book
            var key = $"book:{isbn}";
            await _database.KeyDeleteAsync(key);
            _logger.LogInformation("Deleted book with ISBN {Isbn}", isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting book with ISBN {Isbn}", isbn);
        }
    }

    public async Task UpdateBookStockAsync(string isbn, int totalStock, int availableSellers, decimal minPrice)
    {
        try
        {
            var existingBook = await GetBookByIsbnAsync(isbn);
            if (existingBook == null)
            {
                _logger.LogWarning("Book with ISBN {Isbn} not found for stock update", isbn);
                return;
            }

            existingBook.TotalStock = totalStock;
            existingBook.AvailableSellers = availableSellers;
            existingBook.MinPrice = minPrice;

            // Update the book data
            await AddOrUpdateBookAsync(existingBook);
            
            // Clear page caches since stock changed
            await ClearPageCachesAsync();
            
            _logger.LogInformation("Updated stock for book with ISBN {Isbn}", isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for book with ISBN {Isbn}", isbn);
        }
    }

    private async Task ClearPageCachesAsync()
    {
        try
        {
            // Clear all page caches by pattern
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            await foreach (var key in server.KeysAsync(pattern: "available:page:*", pageSize: 100))
            {
                await _database.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing page caches");
        }
    }

    public async Task<PagedResult<BookSearchModel>> GetAvailableBooksAsync(int page = 1, int pageSize = 20, string? sortBy = null, string? sortOrder = "asc")
    {
        try
        {
            // Cache key for paginated results
            var cacheKey = $"available:page:{page}:size:{pageSize}:sort:{sortBy ?? "default"}:order:{sortOrder}";
            
            // Try to get from cache first (cache for 5 minutes)
            var cachedValue = await _database.StringGetAsync(cacheKey);
            if (!cachedValue.IsNullOrEmpty)
            {
                var cachedResult = JsonSerializer.Deserialize<PagedResult<BookSearchModel>>(cachedValue!);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Cache HIT for page {Page}", page);
                    return cachedResult;
                }
            }

            _logger.LogInformation("Cache MISS for page {Page}, fetching from sorted set", page);

            // Determine which sorted set to use
            string sortedSetKey;
            if (sortBy?.ToLower() == "price")
            {
                sortedSetKey = "available:books:by:price";
            }
            else
            {
                sortedSetKey = "available:books:by:title"; // Default to title
            }

            // Check if sorted set exists
            var setExists = await _database.KeyExistsAsync(sortedSetKey);
            if (!setExists)
            {
                _logger.LogWarning("Sorted set {Key} does not exist! Index may not be built yet.", sortedSetKey);
                return new PagedResult<BookSearchModel> 
                { 
                    Items = Enumerable.Empty<BookSearchModel>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }

            // Get total count from sorted set - O(1) operation
            var totalCount = (int)await _database.SortedSetLengthAsync(sortedSetKey);

            // Get paginated ISBNs from sorted set - SUPER FAST O(log(N) + M)
            var skip = (page - 1) * pageSize;
            RedisValue[] isbnValues;

            if (sortOrder == "desc")
            {
                // Get in descending order
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, -skip - pageSize, -skip - 1, Order.Descending);
            }
            else
            {
                // Get in ascending order
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, skip, skip + pageSize - 1, Order.Ascending);
            }

            if (!isbnValues.Any())
            {
                _logger.LogInformation("No ISBNs found for page {Page}", page);
                return new PagedResult<BookSearchModel> 
                { 
                    Items = Enumerable.Empty<BookSearchModel>(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                };
            }

            _logger.LogInformation("Fetching {Count} books for page {Page} (Total: {Total})", isbnValues.Length, page, totalCount);

            // Fetch books using MGET for maximum performance
            var bookKeys = isbnValues.Select(isbn => (RedisKey)$"book:{isbn}").ToArray();
            var bookValues = await _database.StringGetAsync(bookKeys);

            var books = new List<BookSearchModel>();
            foreach (var value in bookValues)
            {
                if (!value.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(value!);
                    if (book != null)
                    {
                        books.Add(book);
                    }
                }
            }

            var result = new PagedResult<BookSearchModel>
            {
                Items = books,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            // Cache the results for 5 minutes
            await _database.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(5));

            _logger.LogInformation("Returning {Count} books for page {Page} of {TotalPages} (Total: {Total})", 
                books.Count, page, result.TotalPages, totalCount);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available books");
            return new PagedResult<BookSearchModel> 
            { 
                Items = Enumerable.Empty<BookSearchModel>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }
    }


    public async Task<IEnumerable<BookSearchModel>> GetFeaturedBooksAsync(int count = 8)
    {
        try
        {
            var result = await GetAvailableBooksAsync(1, 100); // Get first 100 available books
            var random = new Random();
            
            return result.Items
                .Where(b => b.AvailableSellers > 0)
                .OrderBy(x => random.Next())
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving featured books");
            return Enumerable.Empty<BookSearchModel>();
        }
    }

    public async Task<IEnumerable<SellerInfo>> GetBookSellersAsync(string isbn)
    {
        try
        {
            var key = $"sellers:{isbn}";
            var value = await _database.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return Enumerable.Empty<SellerInfo>();
            }

            var sellers = JsonSerializer.Deserialize<List<SellerInfo>>(value!);
            return sellers ?? Enumerable.Empty<SellerInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sellers for book with ISBN {Isbn}", isbn);
            return Enumerable.Empty<SellerInfo>();
        }
    }

    public async Task<SearchStats> GetSearchStatsAsync()
    {
        try
        {
            var statsKey = "search:stats";
            var value = await _database.StringGetAsync(statsKey);

            if (!value.IsNullOrEmpty)
            {
                return JsonSerializer.Deserialize<SearchStats>(value!) ?? new SearchStats();
            }

            // Calculate stats if not cached
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var bookKeys = server.Keys(pattern: "book:*").ToArray();

            var totalBooks = bookKeys.Length;
            var availableBooks = 0;
            var totalSellers = 0;
            var totalStock = 0;
            var totalPrice = 0m;
            var booksWithPrice = 0;

            var batch = _database.CreateBatch();
            var tasks = bookKeys.Select(key => batch.StringGetAsync(key)).ToArray();
            batch.Execute();

            foreach (var task in tasks)
            {
                var valueBook = await task;
                if (!valueBook.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(valueBook!);
                    if (book != null)
                    {
                        if (book.AvailableSellers > 0)
                        {
                            availableBooks++;
                            totalSellers += book.AvailableSellers;
                            totalStock += book.TotalStock;
                        }
                        if (book.MinPrice > 0)
                        {
                            totalPrice += book.MinPrice;
                            booksWithPrice++;
                        }
                    }
                }
            }

            var stats = new SearchStats
            {
                TotalBooks = totalBooks,
                AvailableBooks = availableBooks,
                TotalSellers = totalSellers,
                TotalStock = totalStock,
                AveragePrice = booksWithPrice > 0 ? totalPrice / booksWithPrice : 0,
                LastUpdated = DateTime.UtcNow
            };

            // Cache stats for 5 minutes
            await _database.StringSetAsync(statsKey, JsonSerializer.Serialize(stats), TimeSpan.FromMinutes(5));

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating search statistics");
            return new SearchStats();
        }
    }

    public async Task UpdateBookWarehouseDataAsync(string isbn, List<WarehouseItem> warehouseItems)
    {
        try
        {
            // Calculate aggregated data
            var availableItems = warehouseItems.Where(item => item.Quantity > 0).ToList();
            var totalStock = warehouseItems.Sum(item => item.Quantity);
            var availableSellers = availableItems.Count;
            var minPrice = availableItems.Any() ? availableItems.Min(item => item.Price) : 0;

            // Update book stock info
            await UpdateBookStockAsync(isbn, totalStock, availableSellers, minPrice);

            // Store seller details
            var sellerInfos = warehouseItems.Select(item => new SellerInfo
            {
                SellerId = item.SellerId,
                Price = item.Price,
                Condition = item.Condition,
                Quantity = item.Quantity,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            var sellersKey = $"sellers:{isbn}";
            await _database.StringSetAsync(sellersKey, JsonSerializer.Serialize(sellerInfos));

            _logger.LogInformation("Updated warehouse data for book {Isbn}: {Sellers} sellers, {Stock} total stock", 
                isbn, availableSellers, totalStock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse data for book {Isbn}", isbn);
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
