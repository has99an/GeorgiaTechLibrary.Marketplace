using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;
using StackExchange.Redis;

namespace SearchService.Infrastructure.Search;

/// <summary>
/// Fuzzy search service using Levenshtein distance for typo tolerance
/// </summary>
public class LevenshteinFuzzySearchService : IFuzzySearchService
{
    private readonly IDatabase _database;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<LevenshteinFuzzySearchService> _logger;
    private const int MaxDistance = 2; // Maximum edit distance for fuzzy matching

    public LevenshteinFuzzySearchService(
        IConnectionMultiplexer redis,
        ISearchIndexService searchIndex,
        ILogger<LevenshteinFuzzySearchService> logger)
    {
        _database = redis.GetDatabase();
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetSimilarTermsAsync(string term, int maxDistance = 2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
                return Enumerable.Empty<string>();

            var termLower = term.ToLowerInvariant();
            var similarTerms = new List<(string Term, int Distance)>();

            // Get all indexed terms (this is simplified - in production, use a more efficient approach)
            // For now, we'll check common misspellings cache
            var cachedSuggestions = await GetCachedSuggestionsAsync(termLower);
            if (cachedSuggestions.Any())
            {
                return cachedSuggestions;
            }

            // Generate phonetic code for the term
            var phonetic = GetPhoneticCode(termLower);
            var phoneticKey = $"phonetic:{phonetic}";
            
            // Get terms with similar phonetic codes
            var phoneticMatches = await _database.SetMembersAsync(phoneticKey);
            
            foreach (var match in phoneticMatches)
            {
                var matchStr = match.ToString();
                var distance = CalculateLevenshteinDistance(termLower, matchStr);
                
                if (distance <= maxDistance && distance > 0)
                {
                    similarTerms.Add((matchStr, distance));
                }
            }

            // Sort by distance (closest first)
            var results = similarTerms
                .OrderBy(t => t.Distance)
                .ThenBy(t => t.Term)
                .Select(t => t.Term)
                .Take(10)
                .ToList();

            // Cache the results
            if (results.Any())
            {
                await CacheSuggestionsAsync(termLower, results);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting similar terms for: {Term}", term);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<ISBN>> FuzzySearchAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<ISBN>();

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var allIsbns = new List<ISBN>();

            foreach (var term in terms)
            {
                // Try exact match first
                var exactResults = await _searchIndex.SearchByTermsAsync(new[] { term.ToLowerInvariant() });
                
                if (exactResults.Any())
                {
                    allIsbns.AddRange(exactResults);
                }
                else
                {
                    // Fall back to fuzzy search
                    var similarTerms = await GetSimilarTermsAsync(term, MaxDistance);
                    
                    foreach (var similarTerm in similarTerms)
                    {
                        var fuzzyResults = await _searchIndex.SearchByTermsAsync(new[] { similarTerm });
                        allIsbns.AddRange(fuzzyResults);
                    }
                }
            }

            return allIsbns.Distinct();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fuzzy search for query: {Query}", query);
            return Enumerable.Empty<ISBN>();
        }
    }

    public async Task IndexTermForFuzzySearchAsync(string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
                return;

            var termLower = term.ToLowerInvariant();
            var phonetic = GetPhoneticCode(termLower);
            var phoneticKey = $"phonetic:{phonetic}";

            // Add term to phonetic index
            await _database.SetAddAsync(phoneticKey, termLower);

            _logger.LogDebug("Indexed term '{Term}' with phonetic code '{Phonetic}'", term, phonetic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing term for fuzzy search: {Term}", term);
        }
    }

    private int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column and row
        for (var i = 0; i <= sourceLength; i++)
            distance[i, 0] = i;

        for (var j = 0; j <= targetLength; j++)
            distance[0, j] = j;

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost
                );
            }
        }

        return distance[sourceLength, targetLength];
    }

    private string GetPhoneticCode(string term)
    {
        // Simplified Soundex-like algorithm
        if (string.IsNullOrEmpty(term))
            return string.Empty;

        var code = new System.Text.StringBuilder();
        var termUpper = term.ToUpperInvariant();

        // Keep first letter
        code.Append(termUpper[0]);

        // Map consonants to digits
        var previousCode = GetPhoneticDigit(termUpper[0]);

        for (var i = 1; i < termUpper.Length && code.Length < 4; i++)
        {
            var currentCode = GetPhoneticDigit(termUpper[i]);

            if (currentCode != '0' && currentCode != previousCode)
            {
                code.Append(currentCode);
                previousCode = currentCode;
            }
        }

        // Pad with zeros
        while (code.Length < 4)
        {
            code.Append('0');
        }

        return code.ToString();
    }

    private char GetPhoneticDigit(char c)
    {
        return c switch
        {
            'B' or 'F' or 'P' or 'V' => '1',
            'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
            'D' or 'T' => '3',
            'L' => '4',
            'M' or 'N' => '5',
            'R' => '6',
            _ => '0' // Vowels and others
        };
    }

    private async Task<IEnumerable<string>> GetCachedSuggestionsAsync(string term)
    {
        try
        {
            var cacheKey = $"fuzzy:suggestions:{term}";
            var cached = await _database.SetMembersAsync(cacheKey);
            return cached.Select(v => v.ToString());
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private async Task CacheSuggestionsAsync(string term, List<string> suggestions)
    {
        try
        {
            var cacheKey = $"fuzzy:suggestions:{term}";
            var values = suggestions.Select(s => (RedisValue)s).ToArray();
            
            await _database.SetAddAsync(cacheKey, values);
            await _database.KeyExpireAsync(cacheKey, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching suggestions for term: {Term}", term);
        }
    }
}

