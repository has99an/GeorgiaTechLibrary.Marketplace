using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using System.Text.Json;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Handler for GetBookSellersQuery
/// </summary>
public class GetBookSellersQueryHandler : IRequestHandler<GetBookSellersQuery, GetBookSellersResult>
{
    private readonly ICacheService _cache;
    private readonly ILogger<GetBookSellersQueryHandler> _logger;

    public GetBookSellersQueryHandler(
        ICacheService cache,
        ILogger<GetBookSellersQueryHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<GetBookSellersResult> Handle(GetBookSellersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting sellers for book ISBN: {Isbn}", request.Isbn);

        var cacheKey = $"sellers:{request.Isbn}";
        var sellersJson = await _cache.GetAsync<string>(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(sellersJson))
        {
            _logger.LogInformation("No sellers found for ISBN: {Isbn}", request.Isbn);
            return new GetBookSellersResult(Enumerable.Empty<SellerInfoDto>());
        }

        var sellers = JsonSerializer.Deserialize<List<SellerInfoDto>>(sellersJson) ?? new List<SellerInfoDto>();

        _logger.LogInformation("Found {Count} sellers for ISBN: {Isbn}", sellers.Count, request.Isbn);

        return new GetBookSellersResult(sellers);
    }
}

