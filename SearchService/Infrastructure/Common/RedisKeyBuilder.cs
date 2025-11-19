using SearchService.Application.Common.Validators;

namespace SearchService.Infrastructure.Common;

/// <summary>
/// Centralized, secure Redis key builder
/// Prevents injection attacks and ensures consistent key patterns
/// </summary>
public static class RedisKeyBuilder
{
    // Key prefixes
    private const string BookPrefix = "book";
    private const string IndexPrefix = "index";
    private const string FacetPrefix = "facet";
    private const string AnalyticsPrefix = "analytics";
    private const string CachePrefix = "cache";
    private const string AutocompletePrefix = "autocomplete";
    private const string PhoneticPrefix = "phonetic";
    private const string RateLimitPrefix = "ratelimit";
    private const string AvailablePrefix = "available";

    /// <summary>
    /// Builds a book key: book:{isbn}
    /// </summary>
    public static string BuildBookKey(string isbn)
    {
        var sanitized = InputSanitizer.SanitizeIsbn(isbn);
        return $"{BookPrefix}:{sanitized}";
    }

    /// <summary>
    /// Builds an index key: index:{term}
    /// </summary>
    public static string BuildIndexKey(string term)
    {
        var sanitized = InputSanitizer.SanitizeRedisKeyComponent(term);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Invalid index term after sanitization", nameof(term));
        
        return $"{IndexPrefix}:{sanitized}";
    }

    /// <summary>
    /// Builds a facet key: facet:{type}:{value}
    /// </summary>
    public static string BuildFacetKey(string facetType, string value)
    {
        var sanitizedType = InputSanitizer.SanitizeRedisKeyComponent(facetType);
        var sanitizedValue = InputSanitizer.SanitizeFacetValue(value);
        
        if (string.IsNullOrWhiteSpace(sanitizedType) || string.IsNullOrWhiteSpace(sanitizedValue))
            throw new ArgumentException("Invalid facet key components after sanitization");
        
        return $"{FacetPrefix}:{sanitizedType}:{sanitizedValue}";
    }

    /// <summary>
    /// Builds an analytics key: analytics:{type}:{term}
    /// </summary>
    public static string BuildAnalyticsKey(string analyticsType, string? term = null)
    {
        var sanitizedType = InputSanitizer.SanitizeRedisKeyComponent(analyticsType);
        
        if (string.IsNullOrWhiteSpace(term))
            return $"{AnalyticsPrefix}:{sanitizedType}";
        
        var sanitizedTerm = InputSanitizer.SanitizeSearchTerm(term, 100);
        return $"{AnalyticsPrefix}:{sanitizedType}:{sanitizedTerm}";
    }

    /// <summary>
    /// Builds a cache key: cache:{queryType}:{hash}
    /// </summary>
    public static string BuildCacheKey(string queryType, string hash)
    {
        var sanitizedType = InputSanitizer.SanitizeRedisKeyComponent(queryType);
        var sanitizedHash = InputSanitizer.SanitizeRedisKeyComponent(hash);
        
        return $"{CachePrefix}:{sanitizedType}:{sanitizedHash}";
    }

    /// <summary>
    /// Builds an autocomplete key: autocomplete:{type}
    /// </summary>
    public static string BuildAutocompleteKey(string type)
    {
        var sanitizedType = InputSanitizer.SanitizeRedisKeyComponent(type);
        return $"{AutocompletePrefix}:{sanitizedType}";
    }

    /// <summary>
    /// Builds a phonetic key: phonetic:{code}
    /// </summary>
    public static string BuildPhoneticKey(string phoneticCode)
    {
        var sanitized = InputSanitizer.SanitizeRedisKeyComponent(phoneticCode);
        return $"{PhoneticPrefix}:{sanitized}";
    }

    /// <summary>
    /// Builds a rate limit key: ratelimit:{clientIp}:{window}
    /// </summary>
    public static string BuildRateLimitKey(string clientIp, string window)
    {
        // Anonymize IP for privacy (keep first 3 octets for IPv4, first 4 groups for IPv6)
        var anonymizedIp = AnonymizeIp(clientIp);
        var sanitizedWindow = InputSanitizer.SanitizeRedisKeyComponent(window);
        
        return $"{RateLimitPrefix}:{anonymizedIp}:{sanitizedWindow}";
    }

    /// <summary>
    /// Builds an available books sorted set key: available:books:by:{field}
    /// </summary>
    public static string BuildAvailableBooksKey(string sortField)
    {
        var sanitized = InputSanitizer.ValidateSortField(sortField);
        return $"{AvailablePrefix}:books:by:{sanitized}";
    }

    /// <summary>
    /// Validates that a key matches expected patterns
    /// </summary>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        // Key should only contain alphanumeric, colons, hyphens, and underscores
        return System.Text.RegularExpressions.Regex.IsMatch(key, @"^[\w:\-]+$");
    }

    /// <summary>
    /// Anonymizes IP address for privacy compliance
    /// </summary>
    private static string AnonymizeIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "unknown";

        // IPv4: Keep first 3 octets, mask last octet
        if (ip.Contains('.'))
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}.0";
            }
        }
        // IPv6: Keep first 4 groups, mask rest
        else if (ip.Contains(':'))
        {
            var parts = ip.Split(':');
            if (parts.Length >= 4)
            {
                return $"{parts[0]}:{parts[1]}:{parts[2]}:{parts[3]}::";
            }
        }

        return InputSanitizer.SanitizeRedisKeyComponent(ip);
    }

    /// <summary>
    /// Builds a fuzzy search suggestions cache key
    /// </summary>
    public static string BuildFuzzySuggestionsKey(string term)
    {
        var sanitized = InputSanitizer.SanitizeSearchTerm(term, 50);
        return $"fuzzy:suggestions:{sanitized}";
    }

    /// <summary>
    /// Builds a popularity key for autocomplete
    /// </summary>
    public static string BuildPopularityKey(string type, string term)
    {
        var sanitizedType = InputSanitizer.SanitizeRedisKeyComponent(type);
        var sanitizedTerm = InputSanitizer.SanitizeSearchTerm(term, 100);
        
        return $"{AutocompletePrefix}:popularity:{sanitizedType}:{sanitizedTerm}";
    }
}

