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
/// </summary>
public record SearchBooksResult(
    PagedResult<BookDto> Books,
    IEnumerable<string>? Suggestions = null // Fuzzy search suggestions
);

