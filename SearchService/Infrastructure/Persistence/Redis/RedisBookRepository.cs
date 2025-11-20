using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Entities;
using SearchService.Domain.Specifications;
using SearchService.Domain.ValueObjects;
using SearchService.Infrastructure.Common;
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
            _logger.LogDebug("Determined sorted set key: {SortedSetKey} (from spec: OrderBy={OrderBy}, OrderByDescending={OrderByDescending})",
                sortedSetKey, spec.OrderBy?.ToString() ?? "null", spec.OrderByDescending?.ToString() ?? "null");
            
            if (string.IsNullOrEmpty(sortedSetKey))
            {
                _logger.LogWarning("No sorted set key determined, falling back to GetAllAsync() and in-memory filtering");
                // Fallback to getting all books and filtering in memory
                var allBooks = await GetAllAsync(cancellationToken);
                return ApplySpecification(allBooks, spec);
            }

            // Check if sorted set exists and get its size
            var sortedSetSize = await _database.SortedSetLengthAsync(sortedSetKey);
            _logger.LogDebug("Sorted set {SortedSetKey} has {Size} members", sortedSetKey, sortedSetSize);

            if (sortedSetSize == 0)
            {
                _logger.LogWarning("Sorted set {SortedSetKey} is empty. No available books found. Books may need stock information to be added to sorted sets.", sortedSetKey);
                return Enumerable.Empty<Book>();
            }

            // Get ISBNs from sorted set
            var skip = spec.Skip;
            var take = spec.Take > 0 ? spec.Take : 20;
            _logger.LogDebug("Querying sorted set: Skip={Skip}, Take={Take}, OrderByDescending={OrderByDescending}",
                skip, take, spec.OrderByDescending != null);
            
            RedisValue[] isbnValues;
            if (spec.OrderByDescending != null)
            {
                var start = -skip - take;
                var stop = -skip - 1;
                _logger.LogDebug("Querying sorted set in descending order: Start={Start}, Stop={Stop}", start, stop);
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, start, stop, Order.Descending);
            }
            else
            {
                var start = skip;
                var stop = skip + take - 1;
                _logger.LogDebug("Querying sorted set in ascending order: Start={Start}, Stop={Stop}", start, stop);
                isbnValues = await _database.SortedSetRangeByRankAsync(sortedSetKey, start, stop, Order.Ascending);
            }

            _logger.LogInformation("Retrieved {Count} ISBNs from sorted set {SortedSetKey}", isbnValues.Length, sortedSetKey);

            if (!isbnValues.Any())
            {
                _logger.LogWarning("No ISBNs retrieved from sorted set {SortedSetKey}. Sorted set may be empty or pagination parameters are out of range.", sortedSetKey);
                return Enumerable.Empty<Book>();
            }

            // Log sample ISBNs for debugging
            var sampleIsbns = isbnValues.Take(5).Select(v => v.ToString()).ToArray();
            _logger.LogDebug("Sample ISBNs retrieved: {SampleIsbns}", string.Join(", ", sampleIsbns));

            // Fetch books
            var isbns = isbnValues.Select(v => ISBN.Create(v.ToString())).ToList();
            _logger.LogDebug("Fetching {Count} books by ISBNs using GetByIsbnsAsync", isbns.Count);
            var books = await GetByIsbnsAsync(isbns, cancellationToken);
            var booksList = books.ToList();
            
            _logger.LogInformation("Successfully retrieved {Count} books from Redis (requested {Requested})", 
                booksList.Count, isbnValues.Length);

            if (booksList.Count < isbnValues.Length)
            {
                _logger.LogWarning("Retrieved fewer books ({Retrieved}) than ISBNs requested ({Requested}). Some books may not exist in Redis.",
                    booksList.Count, isbnValues.Length);
            }

            return booksList;
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
                // Get total count from sorted set (ignores pagination)
                return (int)await _database.SortedSetLengthAsync(sortedSetKey);
            }

            // Fallback: count all matching books (without pagination)
            var allBooks = await GetAllAsync(cancellationToken);
            var query = allBooks.AsQueryable();

            // Apply only criteria filter, NOT pagination
            if (spec.Criteria != null)
            {
                query = query.Where(spec.Criteria);
            }

            return query.Count();
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
                var titleKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
                var priceKey = RedisKeyBuilder.BuildAvailableBooksKey("price");
                
                tasks.Add(batch.SortedSetAddAsync(titleKey, book.Isbn.Value, titleScore));
                
                if (book.Pricing.MinPrice > 0)
                {
                    tasks.Add(batch.SortedSetAddAsync(priceKey, book.Isbn.Value, (double)book.Pricing.MinPrice));
                }
            }
            else
            {
                // Remove from sorted sets if not available
                var titleKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
                var priceKey = RedisKeyBuilder.BuildAvailableBooksKey("price");
                
                tasks.Add(batch.SortedSetRemoveAsync(titleKey, book.Isbn.Value));
                tasks.Add(batch.SortedSetRemoveAsync(priceKey, book.Isbn.Value));
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
            var titleKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
            var priceKey = RedisKeyBuilder.BuildAvailableBooksKey("price");
            
            var tasks = new List<Task>
            {
                batch.KeyDeleteAsync(key),
                batch.SortedSetRemoveAsync(titleKey, isbn.Value),
                batch.SortedSetRemoveAsync(priceKey, isbn.Value)
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

    public async Task<int> RebuildSortedSetsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting to rebuild sorted sets from existing books with stock > 0");

            var titleKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
            var priceKey = RedisKeyBuilder.BuildAvailableBooksKey("price");

            // Clear existing sorted sets
            await _database.KeyDeleteAsync(titleKey);
            await _database.KeyDeleteAsync(priceKey);
            _logger.LogInformation("Cleared existing sorted sets");

            // Get all books from Redis
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var availableBooks = new List<Book>();
            var processedCount = 0;

            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 1000))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                processedCount++;
                var value = await _database.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    var book = DeserializeBook(value!);
                    if (book != null && book.IsAvailable())
                    {
                        availableBooks.Add(book);
                    }
                }

                // Log progress every 1000 books
                if (processedCount % 1000 == 0)
                {
                    _logger.LogInformation("Processed {Processed} books, found {Available} available books so far", 
                        processedCount, availableBooks.Count);
                }
            }

            _logger.LogInformation("Found {Count} available books out of {Total} total books", 
                availableBooks.Count, processedCount);

            if (!availableBooks.Any())
            {
                _logger.LogWarning("No available books found. Sorted sets will remain empty.");
                return 0;
            }

            // Build sorted sets in batches
            const int batchSize = 500;
            var titleEntries = new List<SortedSetEntry>();
            var priceEntries = new List<SortedSetEntry>();

            for (int i = 0; i < availableBooks.Count; i += batchSize)
            {
                var batch = availableBooks.Skip(i).Take(batchSize).ToList();
                
                foreach (var book in batch)
                {
                    var titleScore = GetTitleScore(book.Title);
                    titleEntries.Add(new SortedSetEntry(book.Isbn.Value, titleScore));

                    if (book.Pricing.MinPrice > 0)
                    {
                        priceEntries.Add(new SortedSetEntry(book.Isbn.Value, (double)book.Pricing.MinPrice));
                    }
                }

                // Add batch to sorted sets
                if (titleEntries.Any())
                {
                    await _database.SortedSetAddAsync(titleKey, titleEntries.ToArray());
                    titleEntries.Clear();
                }

                if (priceEntries.Any())
                {
                    await _database.SortedSetAddAsync(priceKey, priceEntries.ToArray());
                    priceEntries.Clear();
                }

                _logger.LogInformation("Processed batch {BatchNumber}, added {Count} books to sorted sets", 
                    (i / batchSize) + 1, batch.Count);
            }

            // Verify final sizes
            var titleSize = await _database.SortedSetLengthAsync(titleKey);
            var priceSize = await _database.SortedSetLengthAsync(priceKey);

            _logger.LogInformation("Successfully rebuilt sorted sets: Title set has {TitleSize} books, Price set has {PriceSize} books", 
                titleSize, priceSize);

            return availableBooks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding sorted sets");
            return 0;
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
                return RedisKeyBuilder.BuildAvailableBooksKey("price");
            }
            
            if (orderExpression.Contains("Title"))
            {
                return RedisKeyBuilder.BuildAvailableBooksKey("title");
            }
        }

        // Default to title
        return RedisKeyBuilder.BuildAvailableBooksKey("title");
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

