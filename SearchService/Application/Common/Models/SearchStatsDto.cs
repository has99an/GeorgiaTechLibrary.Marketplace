namespace SearchService.Application.Common.Models;

/// <summary>
/// Data transfer object for Search Statistics
/// </summary>
public class SearchStatsDto
{
    public int TotalBooks { get; set; }
    public int AvailableBooks { get; set; }
    public int TotalSellers { get; set; }
    public int TotalStock { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTime LastUpdated { get; set; }
}

