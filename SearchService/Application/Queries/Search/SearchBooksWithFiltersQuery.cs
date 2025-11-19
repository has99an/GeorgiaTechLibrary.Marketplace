using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Query to search books with advanced filters
/// </summary>
public record SearchBooksWithFiltersQuery(
    string? SearchTerm,
    List<string>? Genres = null,
    List<string>? Languages = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    double? MinRating = null,
    List<string>? Formats = null,
    List<string>? Conditions = null,
    List<string>? Publishers = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "relevance",
    string? SortOrder = "asc"
) : IRequest<SearchBooksWithFiltersResult>;

/// <summary>
/// Result of filtered search
/// </summary>
public record SearchBooksWithFiltersResult(
    PagedResult<BookDto> Books,
    SearchFacets AppliedFacets
);

