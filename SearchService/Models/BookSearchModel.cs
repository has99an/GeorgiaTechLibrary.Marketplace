namespace SearchService.Models;

public class BookSearchModel
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int YearOfPublication { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string? ImageUrlS { get; set; }
    public string? ImageUrlM { get; set; }
    public string? ImageUrlL { get; set; }
    public int TotalStock { get; set; }
    public int AvailableSellers { get; set; }
    public decimal MinPrice { get; set; }


    public decimal MaxPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTime LastStockUpdate { get; set; }
    public List<string> AvailableConditions { get; set; } = new();
}
