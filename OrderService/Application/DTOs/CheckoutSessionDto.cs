namespace OrderService.Application.DTOs;

/// <summary>
/// DTO representing a checkout session with multi-seller breakdown
/// </summary>
public class CheckoutSessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<SellerAllocationDto> ItemsBySeller { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
    public AddressDto DeliveryAddress { get; set; } = null!;
}
