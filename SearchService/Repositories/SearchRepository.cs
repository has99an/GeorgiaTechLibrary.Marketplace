using System.Text.Json;
using SearchService.Models;
using StackExchange.Redis;

namespace SearchService.Repositories;

public class SearchRepository : ISearchRepository, IDisposable
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SearchRepository> _logger;

    public SearchRepository(IConfiguration configuration, ILogger<SearchRepository> logger)
    {
        _logger = logger;

        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        _redis = ConnectionMultiplexer.Connect(redisConnectionString);
        _database = _redis.GetDatabase();

        _logger.LogInformation("Connected to Redis");
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
            var results = new List<BookSearchModel>();
            var keys = _redis.GetServer(_redis.GetEndPoints().First()).Keys(pattern: "book:*");

            foreach (var key in keys)
            {
                var value = await _database.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    var book = JsonSerializer.Deserialize<BookSearchModel>(value!);
                    if (book != null && (book.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                       book.Author.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(book);
                    }
                }
            }

            return results.OrderBy(b => b.Title);
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
            var key = $"book:{book.Isbn}";
            var json = JsonSerializer.Serialize(book);
            await _database.StringSetAsync(key, json);
            _logger.LogInformation("Added/Updated book with ISBN {Isbn}", book.Isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating book with ISBN {Isbn}", book.Isbn);
        }
    }

    public async Task DeleteBookAsync(string isbn)
    {
        try
        {
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

            await AddOrUpdateBookAsync(existingBook);
            _logger.LogInformation("Updated stock for book with ISBN {Isbn}", isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for book with ISBN {Isbn}", isbn);
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
