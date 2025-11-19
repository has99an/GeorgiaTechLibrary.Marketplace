using SearchService.Domain.Entities;
using SearchService.Domain.ValueObjects;

namespace SearchService.Domain.Services;

/// <summary>
/// Service for managing facet indexes
/// </summary>
public interface IFacetIndexService
{
    /// <summary>
    /// Indexes a book for faceted search
    /// </summary>
    Task IndexBookFacetsAsync(Book book);

    /// <summary>
    /// Removes a book from facet indexes
    /// </summary>
    Task RemoveBookFacetsAsync(ISBN isbn, Book? book = null);

    /// <summary>
    /// Gets ISBNs matching facet filters
    /// </summary>
    Task<IEnumerable<ISBN>> GetBooksByFacetsAsync(
        List<string>? genres = null,
        List<string>? languages = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        double? minRating = null,
        List<string>? formats = null,
        List<string>? conditions = null,
        List<string>? publishers = null
    );

    /// <summary>
    /// Gets facet counts for all available facets
    /// </summary>
    Task<Dictionary<string, Dictionary<string, int>>> GetFacetCountsAsync(IEnumerable<ISBN>? filteredIsbns = null);
}

