using MediatR;
using SearchService.Application.Common.Interfaces;

namespace SearchService.Application.Queries.Analytics;

/// <summary>
/// Handler for GetPopularSearchesQuery
/// </summary>
public class GetPopularSearchesQueryHandler : IRequestHandler<GetPopularSearchesQuery, GetPopularSearchesResult>
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetPopularSearchesQueryHandler> _logger;

    public GetPopularSearchesQueryHandler(
        IAnalyticsRepository analyticsRepository,
        ILogger<GetPopularSearchesQueryHandler> logger)
    {
        _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    public async Task<GetPopularSearchesResult> Handle(GetPopularSearchesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting popular searches: TopN={TopN}, TimeWindow={TimeWindow}",
            request.TopN, request.TimeWindow);

        var searches = await _analyticsRepository.GetPopularSearchesAsync(request.TopN, request.TimeWindow);
        var results = searches.Select(s => new PopularSearch(s.SearchTerm, s.Count)).ToList();

        _logger.LogInformation("Returning {Count} popular searches", results.Count);

        return new GetPopularSearchesResult(results);
    }
}

