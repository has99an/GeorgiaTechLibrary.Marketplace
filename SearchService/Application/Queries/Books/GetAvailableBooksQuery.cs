using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Query to get available books with pagination and sorting
/// </summary>
public record GetAvailableBooksQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortOrder = "asc") : IRequest<GetAvailableBooksResult>;

/// <summary>
/// Result of get available books query
/// Returns individual seller entries instead of aggregated books
/// </summary>
public record GetAvailableBooksResult(PagedResult<BookSellerDto> Books);

