using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Query to get a book by ISBN
/// </summary>
public record GetBookByIsbnQuery(string Isbn) : IRequest<GetBookByIsbnResult>;

/// <summary>
/// Result of get book by ISBN query
/// </summary>
public record GetBookByIsbnResult(BookDto? Book);

