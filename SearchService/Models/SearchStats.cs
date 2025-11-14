namespace SearchService.Models;

public class SearchStats
{
    public int TotalBooks { get; set; }
    public int AvailableBooks { get; set; }
    public int TotalSellers { get; set; }
    public decimal AveragePrice { get; set; }
    public int TotalStock { get; set; }
    public DateTime LastUpdated { get; set; }
}