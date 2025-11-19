namespace SearchService.Application.Common.Interfaces;

/// <summary>
/// Repository for search analytics
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// Tracks a search query
    /// </summary>
    Task TrackSearchAsync(string searchTerm, int resultCount, long responseTimeMs);

    /// <summary>
    /// Tracks a click-through on a search result
    /// </summary>
    Task TrackClickThroughAsync(string searchTerm, string isbn);

    /// <summary>
    /// Gets popular searches
    /// </summary>
    Task<IEnumerable<(string SearchTerm, int Count)>> GetPopularSearchesAsync(int topN = 10, string timeWindow = "24h");

    /// <summary>
    /// Gets searches with zero results
    /// </summary>
    Task<IEnumerable<(string SearchTerm, int Count)>> GetZeroResultSearchesAsync(int topN = 10);

    /// <summary>
    /// Gets overall search statistics
    /// </summary>
    Task<Dictionary<string, object>> GetSearchStatisticsAsync();
}

