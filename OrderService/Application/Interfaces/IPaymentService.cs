namespace OrderService.Application.Interfaces;

/// <summary>
/// Service interface for payment processing
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Processes a payment for an order
    /// </summary>
    Task<PaymentResult> ProcessPaymentAsync(Guid orderId, decimal amount, string paymentMethod = "card");

    /// <summary>
    /// Processes a refund for an order
    /// </summary>
    Task<PaymentResult> ProcessRefundAsync(Guid orderId, decimal amount, string reason);

    /// <summary>
    /// Validates a payment method
    /// </summary>
    Task<bool> ValidatePaymentMethodAsync(string paymentMethod);

    /// <summary>
    /// Gets payment status for an order
    /// </summary>
    Task<PaymentStatus> GetPaymentStatusAsync(Guid orderId);
}

/// <summary>
/// Result of a payment operation
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Payment status information
/// </summary>
public class PaymentStatus
{
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

