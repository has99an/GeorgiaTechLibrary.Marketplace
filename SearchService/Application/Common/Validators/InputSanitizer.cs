using System.Text.RegularExpressions;

namespace SearchService.Application.Common.Validators;

/// <summary>
/// Centralized service for input sanitization and validation
/// Prevents injection attacks and ensures data integrity
/// </summary>
public static class InputSanitizer
{
    private static readonly Regex AlphanumericWithHyphenRegex = new(@"^[\w\-]+$", RegexOptions.Compiled);
    private static readonly Regex IsbnRegex = new(@"^[\d\-]+$", RegexOptions.Compiled);
    private static readonly Regex SafeTextRegex = new(@"[^\w\s\-\.,!?'""]+", RegexOptions.Compiled);
    
    // Dangerous characters that could be used for injection attacks
    private static readonly char[] DangerousChars = { '\n', '\r', '\t', '*', '?', '[', ']', '{', '}', '|', '\\', '/', '<', '>', ';', ':', '`' };
    
    // Whitelisted sort fields
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "relevance", "title", "price", "rating", "author", "year"
    };
    
    // Whitelisted time windows
    private static readonly HashSet<string> AllowedTimeWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        "24h", "7d", "30d", "all"
    };

    /// <summary>
    /// Sanitizes a general text input by removing dangerous characters
    /// </summary>
    public static string SanitizeText(string input, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Trim and limit length
        var sanitized = input.Trim();
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        // Remove dangerous characters
        sanitized = new string(sanitized.Where(c => !DangerousChars.Contains(c)).ToArray());

        // Remove control characters
        sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

        return sanitized;
    }

    /// <summary>
    /// Sanitizes search terms specifically
    /// </summary>
    public static string SanitizeSearchTerm(string searchTerm, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return string.Empty;

        var sanitized = SanitizeText(searchTerm, maxLength);
        
        // Additional search-specific sanitization
        // Remove multiple spaces
        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        
        return sanitized.Trim();
    }

    /// <summary>
    /// Sanitizes ISBN input
    /// </summary>
    public static string SanitizeIsbn(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return string.Empty;

        // Remove all non-digit and non-hyphen characters
        var sanitized = new string(isbn.Where(c => char.IsDigit(c) || c == '-').ToArray());
        
        // Remove hyphens for validation
        var digitsOnly = sanitized.Replace("-", "");
        
        // ISBN should be 10 or 13 digits
        if (digitsOnly.Length != 10 && digitsOnly.Length != 13)
            throw new ArgumentException($"Invalid ISBN length: {digitsOnly.Length}. Must be 10 or 13 digits.");

        return sanitized;
    }

    /// <summary>
    /// Validates and sanitizes Redis key component
    /// </summary>
    public static string SanitizeRedisKeyComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
            return string.Empty;

        // Only allow alphanumeric, hyphens, and underscores
        var sanitized = Regex.Replace(component, @"[^\w\-]", "");
        
        // Limit length to prevent memory issues
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200);

        return sanitized.ToLowerInvariant();
    }

    /// <summary>
    /// Validates sort field against whitelist
    /// </summary>
    public static string ValidateSortField(string? sortBy, string defaultValue = "relevance")
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return defaultValue;

        var sanitized = sortBy.Trim().ToLowerInvariant();
        
        if (!AllowedSortFields.Contains(sanitized))
            return defaultValue;

        return sanitized;
    }

    /// <summary>
    /// Validates sort order
    /// </summary>
    public static string ValidateSortOrder(string? sortOrder, string defaultValue = "asc")
    {
        if (string.IsNullOrWhiteSpace(sortOrder))
            return defaultValue;

        var sanitized = sortOrder.Trim().ToLowerInvariant();
        
        if (sanitized != "asc" && sanitized != "desc")
            return defaultValue;

        return sanitized;
    }

    /// <summary>
    /// Validates time window against whitelist
    /// </summary>
    public static string ValidateTimeWindow(string? timeWindow, string defaultValue = "24h")
    {
        if (string.IsNullOrWhiteSpace(timeWindow))
            return defaultValue;

        var sanitized = timeWindow.Trim().ToLowerInvariant();
        
        if (!AllowedTimeWindows.Contains(sanitized))
            return defaultValue;

        return sanitized;
    }

    /// <summary>
    /// Validates and clamps numeric range
    /// </summary>
    public static int ClampValue(int value, int min, int max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    /// <summary>
    /// Sanitizes autocomplete prefix
    /// </summary>
    public static string SanitizePrefix(string prefix, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        var sanitized = SanitizeText(prefix, maxLength);
        
        // Remove special characters that could cause issues
        sanitized = Regex.Replace(sanitized, @"[^\w\s]", "");
        
        return sanitized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Sanitizes facet values (genre, language, format, etc.)
    /// </summary>
    public static string SanitizeFacetValue(string value, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = SanitizeText(value, maxLength);
        
        // Only allow alphanumeric, spaces, and hyphens
        sanitized = Regex.Replace(sanitized, @"[^\w\s\-]", "");
        
        return sanitized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Sanitizes list of values
    /// </summary>
    public static List<string> SanitizeList(List<string>? values, Func<string, string> sanitizer, int maxItems = 50)
    {
        if (values == null || !values.Any())
            return new List<string>();

        return values
            .Take(maxItems)
            .Select(sanitizer)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Checks if input contains suspicious patterns
    /// </summary>
    public static bool ContainsSuspiciousPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var suspicious = new[]
        {
            "script", "eval", "exec", "cmd", "powershell",
            "../", "..\\", "<?", "?>", "${", "{{",
            "union", "select", "insert", "update", "delete", "drop",
            "javascript:", "data:", "vbscript:"
        };

        var lowerInput = input.ToLowerInvariant();
        return suspicious.Any(pattern => lowerInput.Contains(pattern));
    }

    /// <summary>
    /// Validates ISBN checksum (ISBN-10 and ISBN-13)
    /// </summary>
    public static bool ValidateIsbnChecksum(string isbn)
    {
        var digitsOnly = isbn.Replace("-", "").Replace(" ", "");

        if (digitsOnly.Length == 10)
        {
            return ValidateIsbn10Checksum(digitsOnly);
        }
        else if (digitsOnly.Length == 13)
        {
            return ValidateIsbn13Checksum(digitsOnly);
        }

        return false;
    }

    private static bool ValidateIsbn10Checksum(string isbn)
    {
        if (isbn.Length != 10)
            return false;

        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            if (!char.IsDigit(isbn[i]))
                return false;
            sum += (isbn[i] - '0') * (10 - i);
        }

        // Last character can be X (representing 10)
        char lastChar = isbn[9];
        if (lastChar == 'X' || lastChar == 'x')
            sum += 10;
        else if (char.IsDigit(lastChar))
            sum += (lastChar - '0');
        else
            return false;

        return sum % 11 == 0;
    }

    private static bool ValidateIsbn13Checksum(string isbn)
    {
        if (isbn.Length != 13)
            return false;

        int sum = 0;
        for (int i = 0; i < 13; i++)
        {
            if (!char.IsDigit(isbn[i]))
                return false;

            int digit = isbn[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        return sum % 10 == 0;
    }
}

