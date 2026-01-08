using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a monthly settlement period for a seller
/// Aggregates all payouts for a seller within a time period
/// </summary>
public class SellerSettlement
{
    public Guid SettlementId { get; private set; }
    public string SellerId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public Money TotalPayout { get; private set; }
    public SettlementStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    // Private constructor for EF Core
    private SellerSettlement()
    {
        SellerId = string.Empty;
        TotalPayout = Money.Zero();
        Status = SettlementStatus.Pending;
    }

    private SellerSettlement(
        Guid settlementId,
        string sellerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Money totalPayout)
    {
        SettlementId = settlementId;
        SellerId = sellerId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalPayout = totalPayout;
        Status = SettlementStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new seller settlement
    /// </summary>
    public static SellerSettlement Create(
        string sellerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal totalPayout)
    {
        ValidateSellerId(sellerId);
        ValidatePeriod(periodStart, periodEnd);
        ValidateTotalPayout(totalPayout);

        return new SellerSettlement(
            Guid.NewGuid(),
            sellerId,
            periodStart,
            periodEnd,
            Money.Create(totalPayout));
    }

    /// <summary>
    /// Marks the settlement as processing
    /// </summary>
    public void MarkAsProcessing()
    {
        if (Status != SettlementStatus.Pending)
            throw new InvalidOperationException($"Cannot mark settlement as processing from status {Status}");

        Status = SettlementStatus.Processing;
    }

    /// <summary>
    /// Marks the settlement as paid
    /// </summary>
    public void MarkAsPaid()
    {
        if (Status != SettlementStatus.Processing)
            throw new InvalidOperationException($"Cannot mark settlement as paid from status {Status}");

        Status = SettlementStatus.Paid;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the settlement as failed
    /// </summary>
    public void MarkAsFailed()
    {
        if (Status == SettlementStatus.Paid)
            throw new InvalidOperationException("Cannot mark a paid settlement as failed");

        Status = SettlementStatus.Failed;
    }

    private static void ValidateSellerId(string sellerId)
    {
        if (string.IsNullOrWhiteSpace(sellerId))
            throw new ArgumentException("Seller ID cannot be empty", nameof(sellerId));

        if (sellerId.Length > 100)
            throw new ArgumentException("Seller ID cannot exceed 100 characters", nameof(sellerId));
    }

    private static void ValidatePeriod(DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodStart >= periodEnd)
            throw new ArgumentException("Period start must be before period end");
    }

    private static void ValidateTotalPayout(decimal totalPayout)
    {
        if (totalPayout < 0)
            throw new ArgumentException("Total payout cannot be negative", nameof(totalPayout));
    }
}

/// <summary>
/// Status of a seller settlement
/// </summary>
public enum SettlementStatus
{
    Pending = 0,     // Settlement created, awaiting processing
    Processing = 1,  // Settlement is being processed
    Paid = 2,        // Settlement has been paid to seller
    Failed = 3       // Settlement processing failed
}
