namespace SearchService.Models;

public class BookEvent
{
    public string ISBN { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public int YearOfPublication { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string? ImageUrlS { get; set; }
    public string? ImageUrlM { get; set; }
    public string? ImageUrlL { get; set; }
}
