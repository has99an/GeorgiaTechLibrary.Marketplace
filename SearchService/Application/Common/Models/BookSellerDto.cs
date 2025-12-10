namespace SearchService.Application.Common.Models;

/// <summary>
/// Data transfer object combining Book information with individual Seller information
/// This allows the UI to display each seller as a separate entry, enabling users to choose which seller to buy from
/// </summary>
public class BookSellerDto
{
    // Book information
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
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

    // Seller-specific information
    public string SellerId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}





