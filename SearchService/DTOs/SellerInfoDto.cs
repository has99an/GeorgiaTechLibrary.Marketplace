namespace SearchService.DTOs;

public class SellerInfoDto
{
    public string SellerId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Quantity { get; set; }
}