namespace SearchService.DTOs;

public class SearchResultDto
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
    
    // 👇 DE 8 NYE FELTER
    public string Genre { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public int PageCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string AvailabilityStatus { get; set; } = "Available";
    public string Edition { get; set; } = string.Empty;
    public string Format { get; set; } = "Paperback";
}