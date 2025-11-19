using SearchService.Domain.ValueObjects;

namespace SearchService.Domain.Services;

/// <summary>
/// Service for fuzzy search and typo tolerance
/// </summary>
public interface IFuzzySearchService
{
    /// <summary>
    /// Gets similar terms using Levenshtein distance
    /// </summary>
    Task<IEnumerable<string>> GetSimilarTermsAsync(string term, int maxDistance = 2);

    /// <summary>
    /// Performs fuzzy search with typo tolerance
    /// </summary>
    Task<IEnumerable<ISBN>> FuzzySearchAsync(string query);

    /// <summary>
    /// Indexes a term for fuzzy search
    /// </summary>
    Task IndexTermForFuzzySearchAsync(string term);
}

