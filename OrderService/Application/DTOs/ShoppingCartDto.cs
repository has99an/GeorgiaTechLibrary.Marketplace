namespace OrderService.Application.DTOs;

public class ShoppingCartDto
{
    public Guid ShoppingCartId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}

public class CartItemDto
{
    public Guid CartItemId { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime AddedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

