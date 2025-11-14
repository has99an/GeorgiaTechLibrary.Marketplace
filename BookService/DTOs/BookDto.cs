namespace BookService.DTOs;

public class BookDto
{
    public string ISBN { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public int YearOfPublication { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string? ImageUrlS { get; set; }
    public string? ImageUrlM { get; set; }
    public string? ImageUrlL { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public int PageCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string AvailabilityStatus { get; set; } = "Available";
    public string Edition { get; set; } = string.Empty;
    public string Format { get; set; } = "Paperback";
}