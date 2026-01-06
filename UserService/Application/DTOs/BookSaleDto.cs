namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for BookSale
/// </summary>
public class BookSaleDto
{
    public Guid SaleId { get; set; }
    public Guid ListingId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string BookISBN { get; set; } = string.Empty;
    public Guid SellerId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public DateTime CreatedDate { get; set; }
}




