namespace OrderService.Application.DTOs;

/// <summary>
/// DTO representing a payment allocation record
/// </summary>
public class PaymentAllocationDto
{
    public Guid AllocationId { get; set; }
    public Guid OrderId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal SellerPayout { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidOutAt { get; set; }
}
