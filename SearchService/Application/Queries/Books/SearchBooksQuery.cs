using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Query to search for books by search term with pagination and sorting
/// </summary>
public record SearchBooksQuery(
    string SearchTerm,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "relevance" // relevance, title, price, rating
) : IRequest<SearchBooksResult>;

/// <summary>
/// Result of search books query with pagination
/// Returns individual seller entries - if a book has multiple sellers, each seller appears as a separate entry
/// </summary>
public record SearchBooksResult(
    PagedResult<BookSellerDto> Books,
    IEnumerable<string>? Suggestions = null // Fuzzy search suggestions
);

