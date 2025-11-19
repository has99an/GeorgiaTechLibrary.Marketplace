using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Statistics;

/// <summary>
/// Handler for GetSearchStatsQuery
/// </summary>
public class GetSearchStatsQueryHandler : IRequestHandler<GetSearchStatsQuery, GetSearchStatsResult>
{
    private readonly IBookRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<GetSearchStatsQueryHandler> _logger;

    public GetSearchStatsQueryHandler(
        IBookRepository repository,
        ICacheService cache,
        ILogger<GetSearchStatsQueryHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GetSearchStatsResult> Handle(GetSearchStatsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calculating search statistics");

        // Try to get from cache first
        var cacheKey = "search:stats";
        var cachedStats = await _cache.GetAsync<SearchStatsDto>(cacheKey, cancellationToken);
        
        if (cachedStats != null)
        {
            _logger.LogInformation("Returning cached search statistics");
            return new GetSearchStatsResult(cachedStats);
        }

        // Calculate stats
        var allBooks = await _repository.GetAllAsync(cancellationToken);
        var booksList = allBooks.ToList();

        var stats = new SearchStatsDto
        {
            TotalBooks = booksList.Count,
            AvailableBooks = booksList.Count(b => b.IsAvailable()),
            TotalSellers = booksList.Sum(b => b.Stock.AvailableSellers),
            TotalStock = booksList.Sum(b => b.Stock.TotalStock),
            AveragePrice = booksList.Any(b => b.Pricing.MinPrice > 0)
                ? booksList.Where(b => b.Pricing.MinPrice > 0).Average(b => b.Pricing.MinPrice)
                : 0,
            LastUpdated = DateTime.UtcNow
        };

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5), cancellationToken);

        _logger.LogInformation("Calculated and cached search statistics");

        return new GetSearchStatsResult(stats);
    }
}

