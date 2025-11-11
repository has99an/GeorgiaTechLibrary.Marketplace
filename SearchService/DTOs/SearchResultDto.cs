namespace SearchService.DTOs;

public class SearchResultDto
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public int AvailableSellers { get; set; }
    public decimal MinPrice { get; set; }
}
