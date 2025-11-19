using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Query to get featured/recommended books
/// </summary>
public record GetFeaturedBooksQuery(int Count = 8) : IRequest<GetFeaturedBooksResult>;

/// <summary>
/// Result of get featured books query
/// </summary>
public record GetFeaturedBooksResult(IEnumerable<BookDto> Books);

