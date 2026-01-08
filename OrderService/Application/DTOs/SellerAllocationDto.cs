namespace OrderService.Application.DTOs;

/// <summary>
/// DTO representing payment allocation for a specific seller in a checkout session
/// </summary>
public class SellerAllocationDto
{
    public string SellerId { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new();
    public decimal SellerTotal { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal SellerPayout { get; set; }
    public decimal PlatformFeePercentage { get; set; }
}
