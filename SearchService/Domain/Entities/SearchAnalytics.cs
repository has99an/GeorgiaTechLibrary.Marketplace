namespace SearchService.Domain.Entities;

/// <summary>
/// Domain entity for search analytics
/// </summary>
public class SearchAnalytics
{
    public string SearchTerm { get; private set; }
    public int SearchCount { get; private set; }
    public int ResultCount { get; private set; }
    public DateTime LastSearched { get; private set; }
    public double AverageResponseTime { get; private set; }
    public int ClickThroughCount { get; private set; }

    private SearchAnalytics(
        string searchTerm,
        int searchCount,
        int resultCount,
        DateTime lastSearched,
        double averageResponseTime,
        int clickThroughCount)
    {
        SearchTerm = searchTerm;
        SearchCount = searchCount;
        ResultCount = resultCount;
        LastSearched = lastSearched;
        AverageResponseTime = averageResponseTime;
        ClickThroughCount = clickThroughCount;
    }

    public static SearchAnalytics Create(string searchTerm)
    {
        return new SearchAnalytics(
            searchTerm,
            0,
            0,
            DateTime.UtcNow,
            0,
            0
        );
    }

    public void RecordSearch(int resultCount, long responseTimeMs)
    {
        SearchCount++;
        ResultCount = resultCount;
        LastSearched = DateTime.UtcNow;
        
        // Calculate running average
        AverageResponseTime = ((AverageResponseTime * (SearchCount - 1)) + responseTimeMs) / SearchCount;
    }

    public void RecordClickThrough()
    {
        ClickThroughCount++;
    }

    public double GetClickThroughRate()
    {
        return SearchCount > 0 ? (double)ClickThroughCount / SearchCount * 100 : 0;
    }
}

