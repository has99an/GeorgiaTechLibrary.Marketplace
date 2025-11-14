namespace SearchService.Models;

public class SellerInfo
{
    public string SellerId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
}