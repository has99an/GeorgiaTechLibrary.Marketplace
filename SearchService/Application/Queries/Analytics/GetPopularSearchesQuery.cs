using MediatR;

namespace SearchService.Application.Queries.Analytics;

/// <summary>
/// Query to get popular searches
/// </summary>
public record GetPopularSearchesQuery(int TopN = 10, string TimeWindow = "24h") : IRequest<GetPopularSearchesResult>;

/// <summary>
/// Result containing popular searches
/// </summary>
public record GetPopularSearchesResult(IEnumerable<PopularSearch> Searches);

/// <summary>
/// Popular search item
/// </summary>
public record PopularSearch(string SearchTerm, int Count);

