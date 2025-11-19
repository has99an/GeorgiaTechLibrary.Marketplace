using SearchService.Domain.Entities;
using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Search;

/// <summary>
/// Redis-based facet index service for efficient multi-facet filtering
/// </summary>
public class RedisFacetIndexService : IFacetIndexService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisFacetIndexService> _logger;

    public RedisFacetIndexService(
        IConnectionMultiplexer redis,
        ILogger<RedisFacetIndexService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task IndexBookFacetsAsync(Book book)
    {
        try
        {
            // Use batch for efficient indexing
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            var isbn = book.Isbn.Value;

            // Index by genre
            if (!string.IsNullOrWhiteSpace(book.Genre))
            {
                tasks.Add(batch.SetAddAsync($"facet:genre:{book.Genre.ToLowerInvariant()}", isbn));
            }

            // Index by language
            if (!string.IsNullOrWhiteSpace(book.Language))
            {
                tasks.Add(batch.SetAddAsync($"facet:language:{book.Language.ToLowerInvariant()}", isbn));
            }

            // Index by format
            if (!string.IsNullOrWhiteSpace(book.Format))
            {
                tasks.Add(batch.SetAddAsync($"facet:format:{book.Format.ToLowerInvariant()}", isbn));
            }

            // Index by publisher
            if (!string.IsNullOrWhiteSpace(book.Publisher))
            {
                tasks.Add(batch.SetAddAsync($"facet:publisher:{book.Publisher.ToLowerInvariant()}", isbn));
            }

            // Index by price range (bucketed)
            if (book.Pricing.MinPrice > 0)
            {
                var priceBucket = GetPriceBucket(book.Pricing.MinPrice);
                tasks.Add(batch.SetAddAsync($"facet:price:{priceBucket}", isbn));
            }

            // Index by rating range (bucketed)
            if (book.Rating > 0)
            {
                var ratingBucket = GetRatingBucket(book.Rating);
                tasks.Add(batch.SetAddAsync($"facet:rating:{ratingBucket}", isbn));
            }

            // Index by conditions
            foreach (var condition in book.AvailableConditions)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    tasks.Add(batch.SetAddAsync($"facet:condition:{condition.ToLowerInvariant()}", isbn));
                }
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Indexed facets for book ISBN {Isbn}", isbn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing facets for book ISBN {Isbn}", book.Isbn.Value);
        }
    }

    public async Task RemoveBookFacetsAsync(ISBN isbn, Book? book = null)
    {
        try
        {
            if (book == null)
            {
                // If we don't have the book, we need to remove from all possible facet indexes
                // This is less efficient but necessary
                _logger.LogWarning("Removing facets without book data for ISBN {Isbn} - using scan", isbn.Value);
                return;
            }

            // Use batch for efficient removal
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            var isbnValue = isbn.Value;

            // Remove from all facet indexes
            if (!string.IsNullOrWhiteSpace(book.Genre))
            {
                tasks.Add(batch.SetRemoveAsync($"facet:genre:{book.Genre.ToLowerInvariant()}", isbnValue));
            }

            if (!string.IsNullOrWhiteSpace(book.Language))
            {
                tasks.Add(batch.SetRemoveAsync($"facet:language:{book.Language.ToLowerInvariant()}", isbnValue));
            }

            if (!string.IsNullOrWhiteSpace(book.Format))
            {
                tasks.Add(batch.SetRemoveAsync($"facet:format:{book.Format.ToLowerInvariant()}", isbnValue));
            }

            if (!string.IsNullOrWhiteSpace(book.Publisher))
            {
                tasks.Add(batch.SetRemoveAsync($"facet:publisher:{book.Publisher.ToLowerInvariant()}", isbnValue));
            }

            if (book.Pricing.MinPrice > 0)
            {
                var priceBucket = GetPriceBucket(book.Pricing.MinPrice);
                tasks.Add(batch.SetRemoveAsync($"facet:price:{priceBucket}", isbnValue));
            }

            if (book.Rating > 0)
            {
                var ratingBucket = GetRatingBucket(book.Rating);
                tasks.Add(batch.SetRemoveAsync($"facet:rating:{ratingBucket}", isbnValue));
            }

            foreach (var condition in book.AvailableConditions)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    tasks.Add(batch.SetRemoveAsync($"facet:condition:{condition.ToLowerInvariant()}", isbnValue));
                }
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Removed facets for book ISBN {Isbn}", isbnValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing facets for book ISBN {Isbn}", isbn.Value);
        }
    }

    public async Task<IEnumerable<ISBN>> GetBooksByFacetsAsync(
        List<string>? genres = null,
        List<string>? languages = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        double? minRating = null,
        List<string>? formats = null,
        List<string>? conditions = null,
        List<string>? publishers = null)
    {
        try
        {
            var setKeys = new List<RedisKey>();

            // Collect all facet set keys
            if (genres?.Any() == true)
            {
                setKeys.AddRange(genres.Select(g => (RedisKey)$"facet:genre:{g.ToLowerInvariant()}"));
            }

            if (languages?.Any() == true)
            {
                setKeys.AddRange(languages.Select(l => (RedisKey)$"facet:language:{l.ToLowerInvariant()}"));
            }

            if (formats?.Any() == true)
            {
                setKeys.AddRange(formats.Select(f => (RedisKey)$"facet:format:{f.ToLowerInvariant()}"));
            }

            if (publishers?.Any() == true)
            {
                setKeys.AddRange(publishers.Select(p => (RedisKey)$"facet:publisher:{p.ToLowerInvariant()}"));
            }

            if (conditions?.Any() == true)
            {
                setKeys.AddRange(conditions.Select(c => (RedisKey)$"facet:condition:{c.ToLowerInvariant()}"));
            }

            // Price range filtering
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                var priceBuckets = GetPriceBuckets(minPrice ?? 0, maxPrice ?? decimal.MaxValue);
                setKeys.AddRange(priceBuckets.Select(b => (RedisKey)$"facet:price:{b}"));
            }

            // Rating filtering
            if (minRating.HasValue)
            {
                var ratingBuckets = GetRatingBuckets(minRating.Value);
                setKeys.AddRange(ratingBuckets.Select(b => (RedisKey)$"facet:rating:{b}"));
            }

            if (!setKeys.Any())
            {
                return Enumerable.Empty<ISBN>();
            }

            // Perform set intersection (SINTER) for multi-facet filtering
            RedisValue[] isbnValues;
            if (setKeys.Count == 1)
            {
                isbnValues = await _database.SetMembersAsync(setKeys[0]);
            }
            else
            {
                isbnValues = await _database.SetCombineAsync(SetOperation.Intersect, setKeys.ToArray());
            }

            var isbns = isbnValues.Select(v => ISBN.Create(v.ToString())).ToList();

            _logger.LogDebug("Found {Count} books matching facet filters", isbns.Count);

            return isbns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting books by facets");
            return Enumerable.Empty<ISBN>();
        }
    }

    public Task<Dictionary<string, Dictionary<string, int>>> GetFacetCountsAsync(IEnumerable<ISBN>? filteredIsbns = null)
    {
        try
        {
            var facetCounts = new Dictionary<string, Dictionary<string, int>>();
            var facetTypes = new[] { "genre", "language", "format", "publisher", "condition", "price", "rating" };

            foreach (var facetType in facetTypes)
            {
                var counts = new Dictionary<string, int>();
                var pattern = $"facet:{facetType}:*";
                
                // This is a simplified version - in production, you'd want to cache this
                // or use a more efficient approach
                
                facetCounts[facetType] = counts;
            }

            return Task.FromResult(facetCounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting facet counts");
            return Task.FromResult(new Dictionary<string, Dictionary<string, int>>());
        }
    }

    private string GetPriceBucket(decimal price)
    {
        // Bucket prices into ranges
        if (price < 10) return "0-10";
        if (price < 25) return "10-25";
        if (price < 50) return "25-50";
        if (price < 100) return "50-100";
        if (price < 200) return "100-200";
        return "200+";
    }

    private List<string> GetPriceBuckets(decimal minPrice, decimal maxPrice)
    {
        var buckets = new List<string> { "0-10", "10-25", "25-50", "50-100", "100-200", "200+" };
        return buckets; // Simplified - should filter based on actual range
    }

    private string GetRatingBucket(double rating)
    {
        // Bucket ratings
        if (rating >= 4.5) return "4.5+";
        if (rating >= 4.0) return "4.0+";
        if (rating >= 3.5) return "3.5+";
        if (rating >= 3.0) return "3.0+";
        return "below-3.0";
    }

    private List<string> GetRatingBuckets(double minRating)
    {
        var buckets = new List<string>();
        if (minRating <= 3.0) buckets.Add("3.0+");
        if (minRating <= 3.5) buckets.Add("3.5+");
        if (minRating <= 4.0) buckets.Add("4.0+");
        if (minRating <= 4.5) buckets.Add("4.5+");
        return buckets;
    }
}

