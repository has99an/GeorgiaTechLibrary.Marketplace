using OrderService.Application.DTOs;

namespace OrderService.Application.Models;

/// <summary>
/// Internal model representing a checkout session stored in Redis
/// </summary>
public class CheckoutSession
{
    public string SessionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public Guid ShoppingCartId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<SellerAllocationDto> ItemsBySeller { get; set; } = new();
    public AddressDto DeliveryAddress { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
