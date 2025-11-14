namespace WarehouseService.DTOs;

public class WarehouseItemDto
{
    public int Id { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsNew { get; set; }
}