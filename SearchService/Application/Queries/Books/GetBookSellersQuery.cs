using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Query to get all sellers offering a specific book
/// </summary>
public record GetBookSellersQuery(string Isbn) : IRequest<GetBookSellersResult>;

/// <summary>
/// Result of get book sellers query
/// </summary>
public record GetBookSellersResult(IEnumerable<SellerInfoDto> Sellers);

