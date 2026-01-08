namespace OrderService.Application.DTOs;

/// <summary>
/// DTO representing a seller settlement record
/// </summary>
public class SellerSettlementDto
{
    public Guid SettlementId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal TotalPayout { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
