namespace SearchService.Models;

public class WarehouseItem
{
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
}