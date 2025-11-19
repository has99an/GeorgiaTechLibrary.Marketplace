using SearchService.Domain.Services;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Search;

/// <summary>
/// Redis-based autocomplete service using sorted sets and prefix matching
/// </summary>
public class RedisAutocompleteService : IAutocompleteService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisAutocompleteService> _logger;
    private const string AutocompletePrefix = "autocomplete:";
    private const string PopularityPrefix = "autocomplete:popularity:";

    public RedisAutocompleteService(
        IConnectionMultiplexer redis,
        ILogger<RedisAutocompleteService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<IEnumerable<(string Text, string Type, double Score)>> GetSuggestionsAsync(string prefix, int maxResults = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return Enumerable.Empty<(string, string, double)>();

            var prefixLower = prefix.ToLowerInvariant();
            var suggestions = new List<(string Text, string Type, double Score)>();

            // Search in different autocomplete indexes (title, author, isbn)
            var types = new[] { "title", "author", "isbn" };
            
            foreach (var type in types)
            {
                var key = $"{AutocompletePrefix}{type}";
                
                // Use ZRANGEBYLEX for prefix matching
                var results = await _database.SortedSetRangeByValueAsync(
                    key,
                    min: prefixLower,
                    max: prefixLower + "\xff",
                    take: maxResults
                );

                foreach (var result in results)
                {
                    var text = result.ToString();
                    
                    // Get popularity score
                    var popularityKey = $"{PopularityPrefix}{type}:{text}";
                    var popularityScore = (double)await _database.StringGetAsync(popularityKey);
                    
                    // Base score + popularity
                    var score = 1.0 + popularityScore;
                    
                    suggestions.Add((text, type, score));
                }
            }

            // Sort by score and return top N
            var topSuggestions = suggestions
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Text.Length) // Prefer shorter matches
                .Take(maxResults)
                .ToList();

            _logger.LogDebug("Found {Count} autocomplete suggestions for prefix: {Prefix}", 
                topSuggestions.Count, prefix);

            return topSuggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting autocomplete suggestions for prefix: {Prefix}", prefix);
            return Enumerable.Empty<(string, string, double)>();
        }
    }

    public async Task IndexTermAsync(string term, string type, double score = 1.0)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
                return;

            var termLower = term.ToLowerInvariant();
            var key = $"{AutocompletePrefix}{type}";

            // Add to sorted set with score 0 (we use ZRANGEBYLEX, not ZRANGEBYSCORE)
            await _database.SortedSetAddAsync(key, termLower, 0);

            _logger.LogDebug("Indexed term '{Term}' for autocomplete type '{Type}'", term, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing term '{Term}' for autocomplete", term);
        }
    }

    public async Task RecordSelectionAsync(string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
                return;

            var termLower = term.ToLowerInvariant();
            
            // Increment popularity across all types
            var types = new[] { "title", "author", "isbn" };
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var type in types)
            {
                var popularityKey = $"{PopularityPrefix}{type}:{termLower}";
                tasks.Add(batch.StringIncrementAsync(popularityKey));
                tasks.Add(batch.KeyExpireAsync(popularityKey, TimeSpan.FromDays(30)));
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Recorded selection for term: {Term}", term);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording selection for term: {Term}", term);
        }
    }
}

