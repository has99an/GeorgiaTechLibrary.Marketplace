using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing payment allocation to a seller for an order
/// Tracks how revenue is split between platform and seller
/// </summary>
public class PaymentAllocation
{
    public Guid AllocationId { get; private set; }
    public Guid OrderId { get; private set; }
    public string SellerId { get; private set; }
    public Money TotalAmount { get; private set; }
    public Money PlatformFee { get; private set; }
    public Money SellerPayout { get; private set; }
    public PaymentAllocationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidOutAt { get; private set; }

    // Navigation property
    public Order Order { get; private set; } = null!;

    // Private constructor for EF Core
    private PaymentAllocation()
    {
        SellerId = string.Empty;
        TotalAmount = Money.Zero();
        PlatformFee = Money.Zero();
        SellerPayout = Money.Zero();
        Status = PaymentAllocationStatus.Pending;
    }

    private PaymentAllocation(
        Guid allocationId,
        Guid orderId,
        string sellerId,
        Money totalAmount,
        Money platformFee,
        Money sellerPayout)
    {
        AllocationId = allocationId;
        OrderId = orderId;
        SellerId = sellerId;
        TotalAmount = totalAmount;
        PlatformFee = platformFee;
        SellerPayout = sellerPayout;
        Status = PaymentAllocationStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new payment allocation
    /// </summary>
    public static PaymentAllocation Create(
        Guid orderId,
        string sellerId,
        decimal totalAmount,
        decimal platformFeePercentage)
    {
        ValidateSellerId(sellerId);
        ValidateTotalAmount(totalAmount);
        ValidatePlatformFeePercentage(platformFeePercentage);

        var totalMoney = Money.Create(totalAmount);
        var platformFeeAmount = totalAmount * (platformFeePercentage / 100m);
        var platformFeeMoney = Money.Create(platformFeeAmount);
        var sellerPayoutAmount = totalAmount - platformFeeAmount;
        var sellerPayoutMoney = Money.Create(sellerPayoutAmount);

        return new PaymentAllocation(
            Guid.NewGuid(),
            orderId,
            sellerId,
            totalMoney,
            platformFeeMoney,
            sellerPayoutMoney);
    }

    /// <summary>
    /// Marks the allocation as paid out to the seller
    /// </summary>
    public void MarkAsPaidOut()
    {
        if (Status == PaymentAllocationStatus.PaidOut)
            throw new InvalidOperationException("Allocation is already marked as paid out");

        if (Status == PaymentAllocationStatus.Cancelled)
            throw new InvalidOperationException("Cannot mark a cancelled allocation as paid out");

        Status = PaymentAllocationStatus.PaidOut;
        PaidOutAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the allocation as cancelled (e.g., due to order cancellation/refund)
    /// </summary>
    public void MarkAsCancelled()
    {
        if (Status == PaymentAllocationStatus.PaidOut)
            throw new InvalidOperationException("Cannot cancel an allocation that has been paid out");

        if (Status == PaymentAllocationStatus.Cancelled)
            throw new InvalidOperationException("Allocation is already cancelled");

        Status = PaymentAllocationStatus.Cancelled;
    }

    private static void ValidateSellerId(string sellerId)
    {
        if (string.IsNullOrWhiteSpace(sellerId))
            throw new ArgumentException("Seller ID cannot be empty", nameof(sellerId));

        if (sellerId.Length > 100)
            throw new ArgumentException("Seller ID cannot exceed 100 characters", nameof(sellerId));
    }

    private static void ValidateTotalAmount(decimal totalAmount)
    {
        if (totalAmount <= 0)
            throw new ArgumentException("Total amount must be greater than zero", nameof(totalAmount));
    }

    private static void ValidatePlatformFeePercentage(decimal platformFeePercentage)
    {
        if (platformFeePercentage < 0 || platformFeePercentage > 100)
            throw new ArgumentException("Platform fee percentage must be between 0 and 100", nameof(platformFeePercentage));
    }
}

/// <summary>
/// Status of a payment allocation
/// </summary>
public enum PaymentAllocationStatus
{
    Pending = 0,    // Allocation created, awaiting settlement
    PaidOut = 1,    // Seller has been paid
    Cancelled = 2   // Allocation cancelled (order refunded/cancelled)
}
