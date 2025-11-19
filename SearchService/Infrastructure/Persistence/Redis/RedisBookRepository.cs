using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Entities;
using SearchService.Domain.Specifications;
using SearchService.Domain.ValueObjects;
using StackExchange.Redis;
using System.Text.Json;

namespace SearchService.Infrastructure.Persistence.Redis;

/// <summary>
/// Redis implementation of IBookRepository
/// </summary>
public class RedisBookRepository : IBookRepository
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisBookRepository> _logger;

    public RedisBookRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisBookRepository> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<Book?> GetByIsbnAsync(ISBN isbn, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"book:{isbn.Value}";
            var value = await _database.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return DeserializeBook(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving book with ISBN {Isbn}", isbn.Value);
            return null;
        }
    }

    public async Task<IEnumerable<Book>> GetByIsbnsAsync(IEnumerable<ISBN> isbns, CancellationToken cancellationToken = default)
    {
        try
        {
            var isbnList = isbns.ToList();
            
            if (!isbnList.Any())
                return Enumerable.Empty<Book>();

            // Use MGET for efficient batch retrieval (single round-trip to Redis)
            var keys = isbnList.Select(isbn => (RedisKey)$"book:{isbn.Value}").ToArray();
            var values = await _database.StringGetAsync(keys);
            
            // Parallel deserialization for better performance
            var books = new List<Book>(values.Length);
            var deserializationTasks = values
                .Where(v => !v.IsNullOrEmpty)
                .Select(async value =>
                {
                    return await Task.Run(() => DeserializeBook(value!), cancellationToken);
                });

            var deserializedBooks = await Task.WhenAll(deserializationTasks);
            books.AddRange(deserializedBooks.Where(b => b != null)!);

            _logger.LogDebug("Retrieved {Count} books out of {Requested} requested ISBNs using MGET", 
                books.Count, isbnList.Count);

            return books;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving books by ISBNs");
            return Enumerable.Empty<Book>();
        }
    }

    public async Task<IEnumerable<Book>> GetAsync(ISpecification<Book> spec, CancellationToken cancellationToken = default)
    {
        try
        {
            // For Redis, we need to get ISBNs from sorted sets based on specification
            var sortedSetKey = DetermineSortedSetKey(spec);
            
            if (string.IsNullOrEmpty(sortedSetKey))
            {
                // Fallback to getting all books and filtering in memory
                var allBooks = await GetAllAsync(cancellationToken);
                return ApplySpecification(allBooks, spec);
            }

            // Get ISBNs from sorted set
            var skip = spec.Skip;
            var take = spec.Take > 0 ? spec.Take : 20;
            
            RedisValue[] isbnValues;
            if (spec.OrderByDescending != null)
            {
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, -skip - take, -skip - 1, Order.Descending);
            }
            else
            {
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, skip, skip + take - 1, Order.Ascending);
            }

            if (!isbnValues.Any())
                return Enumerable.Empty<Book>();

            // Fetch books
            var isbns = isbnValues.Select(v => ISBN.Create(v.ToString())).ToList();
            return await GetByIsbnsAsync(isbns, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving books with specification");
            return Enumerable.Empty<Book>();
        }
    }

    public async Task<int> CountAsync(ISpecification<Book> spec, CancellationToken cancellationToken = default)
    {
        try
        {
            var sortedSetKey = DetermineSortedSetKey(spec);
            
            if (!string.IsNullOrEmpty(sortedSetKey))
            {
                return (int)await _database.SortedSetLengthAsync(sortedSetKey);
            }

            // Fallback: count all matching books
            var allBooks = await GetAllAsync(cancellationToken);
            return ApplySpecification(allBooks, spec).Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting books with specification");
            return 0;
        }
    }

    public async Task AddOrUpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"book:{book.Isbn.Value}";
            var json = SerializeBook(book);
            
            // Use Redis batch/pipeline for multiple operations (single round-trip)
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            // Set book data
            tasks.Add(batch.StringSetAsync(key, json));

            // Update sorted sets if available
            if (book.IsAvailable())
            {
                var titleScore = GetTitleScore(book.Title);
                tasks.Add(batch.SortedSetAddAsync("available:books:by:title", book.Isbn.Value, titleScore));
                
                if (book.Pricing.MinPrice > 0)
                {
                    tasks.Add(batch.SortedSetAddAsync("available:books:by:price", book.Isbn.Value, (double)book.Pricing.MinPrice));
                }
            }
            else
            {
                // Remove from sorted sets if not available
                tasks.Add(batch.SortedSetRemoveAsync("available:books:by:title", book.Isbn.Value));
                tasks.Add(batch.SortedSetRemoveAsync("available:books:by:price", book.Isbn.Value));
            }

            // Execute batch
            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation("Added/Updated book with ISBN {Isbn} using batch operation", book.Isbn.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating book with ISBN {Isbn}", book.Isbn.Value);
        }
    }

    public async Task DeleteAsync(ISBN isbn, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"book:{isbn.Value}";
            
            // Use Redis batch for multiple operations (single round-trip)
            var batch = _database.CreateBatch();
            var tasks = new List<Task>
            {
                batch.KeyDeleteAsync(key),
                batch.SortedSetRemoveAsync("available:books:by:title", isbn.Value),
                batch.SortedSetRemoveAsync("available:books:by:price", isbn.Value)
            };

            // Execute batch
            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation("Deleted book with ISBN {Isbn} using batch operation", isbn.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting book with ISBN {Isbn}", isbn.Value);
        }
    }

    public async Task<IEnumerable<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var books = new List<Book>();

            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 1000))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var value = await _database.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    var book = DeserializeBook(value!);
                    if (book != null)
                    {
                        books.Add(book);
                    }
                }
            }

            return books;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all books");
            return Enumerable.Empty<Book>();
        }
    }

    private string SerializeBook(Book book)
    {
        var dto = new
        {
            isbn = book.Isbn.Value,
            title = book.Title,
            author = book.Author,
            yearOfPublication = book.YearOfPublication,
            publisher = book.Publisher,
            imageUrlS = book.ImageUrlS,
            imageUrlM = book.ImageUrlM,
            imageUrlL = book.ImageUrlL,
            totalStock = book.Stock.TotalStock,
            availableSellers = book.Stock.AvailableSellers,
            minPrice = book.Pricing.MinPrice,
            maxPrice = book.Pricing.MaxPrice,
            averagePrice = book.Pricing.AveragePrice,
            availableConditions = book.AvailableConditions,
            genre = book.Genre,
            language = book.Language,
            pageCount = book.PageCount,
            description = book.Description,
            rating = book.Rating,
            availabilityStatus = book.AvailabilityStatus,
            edition = book.Edition,
            format = book.Format
        };

        return JsonSerializer.Serialize(dto);
    }

    private Book? DeserializeBook(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var isbn = ISBN.Create(root.GetProperty("isbn").GetString() ?? "");
            var book = Book.Create(
                isbn,
                root.GetProperty("title").GetString() ?? "",
                root.GetProperty("author").GetString() ?? "",
                root.GetProperty("yearOfPublication").GetInt32(),
                root.GetProperty("publisher").GetString() ?? "",
                root.TryGetProperty("imageUrlS", out var imgS) ? imgS.GetString() : null,
                root.TryGetProperty("imageUrlM", out var imgM) ? imgM.GetString() : null,
                root.TryGetProperty("imageUrlL", out var imgL) ? imgL.GetString() : null,
                root.TryGetProperty("genre", out var genre) ? genre.GetString() ?? "" : "",
                root.TryGetProperty("language", out var lang) ? lang.GetString() ?? "English" : "English",
                root.TryGetProperty("pageCount", out var pages) ? pages.GetInt32() : 0,
                root.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                root.TryGetProperty("rating", out var rating) ? rating.GetDouble() : 0.0,
                root.TryGetProperty("availabilityStatus", out var avail) ? avail.GetString() ?? "Available" : "Available",
                root.TryGetProperty("edition", out var ed) ? ed.GetString() ?? "" : "",
                root.TryGetProperty("format", out var fmt) ? fmt.GetString() ?? "Paperback" : "Paperback"
            );

            // Update stock and pricing
            var totalStock = root.GetProperty("totalStock").GetInt32();
            var availableSellers = root.GetProperty("availableSellers").GetInt32();
            var minPrice = root.GetProperty("minPrice").GetDecimal();
            
            book.UpdateStock(totalStock, availableSellers, minPrice);

            if (root.TryGetProperty("maxPrice", out var maxPrice) && root.TryGetProperty("averagePrice", out var avgPrice))
            {
                book.UpdatePricing(minPrice, maxPrice.GetDecimal(), avgPrice.GetDecimal());
            }

            if (root.TryGetProperty("availableConditions", out var conditions))
            {
                var conditionsList = conditions.EnumerateArray().Select(c => c.GetString() ?? "").ToList();
                book.UpdateConditions(conditionsList);
            }

            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing book");
            return null;
        }
    }

    private string DetermineSortedSetKey(ISpecification<Book> spec)
    {
        // Determine which sorted set to use based on specification
        if (spec.OrderBy != null || spec.OrderByDescending != null)
        {
            var orderExpression = (spec.OrderBy ?? spec.OrderByDescending)?.ToString() ?? "";
            
            if (orderExpression.Contains("MinPrice") || orderExpression.Contains("Pricing"))
            {
                return "available:books:by:price";
            }
            
            if (orderExpression.Contains("Title"))
            {
                return "available:books:by:title";
            }
        }

        // Default to title
        return "available:books:by:title";
    }

    private IEnumerable<Book> ApplySpecification(IEnumerable<Book> books, ISpecification<Book> spec)
    {
        var query = books.AsQueryable();

        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }

        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip).Take(spec.Take);
        }

        return query.ToList();
    }

    private double GetTitleScore(string title)
    {
        var titleLower = title.ToLowerInvariant().PadRight(8, 'z');
        var score = 0.0;
        
        for (int i = 0; i < Math.Min(8, titleLower.Length); i++)
        {
            score = score * 128 + (int)titleLower[i];
        }
        
        return score;
    }
}

