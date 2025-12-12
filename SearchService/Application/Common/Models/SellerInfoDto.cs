namespace SearchService.Application.Common.Models;

/// <summary>
/// Data transfer object for Seller Information
/// </summary>
public class SellerInfoDto
{
    public string SellerId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? Location { get; set; } // Optional for backward compatibility
    public string? SellerName { get; set; } // Seller name for display in UI
}

