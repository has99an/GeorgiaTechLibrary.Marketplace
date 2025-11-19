using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Payment;

/// <summary>
/// Mock payment service for testing and development
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;
    private readonly Dictionary<Guid, PaymentResult> _paymentHistory = new();

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    public Task<PaymentResult> ProcessPaymentAsync(Guid orderId, decimal amount, string paymentMethod = "card")
    {
        _logger.LogInformation("Processing mock payment for order {OrderId}, amount: {Amount}", orderId, amount);

        var result = new PaymentResult
        {
            Success = true,
            TransactionId = $"MOCK-{Guid.NewGuid():N}",
            Message = "Payment processed successfully (mock)",
            ProcessedAt = DateTime.UtcNow,
            Amount = amount
        };

        _paymentHistory[orderId] = result;

        _logger.LogInformation("Mock payment successful: {TransactionId}", result.TransactionId);

        return Task.FromResult(result);
    }

    public Task<PaymentResult> ProcessRefundAsync(Guid orderId, decimal amount, string reason)
    {
        _logger.LogInformation("Processing mock refund for order {OrderId}, amount: {Amount}, reason: {Reason}", 
            orderId, amount, reason);

        var result = new PaymentResult
        {
            Success = true,
            TransactionId = $"REFUND-{Guid.NewGuid():N}",
            Message = $"Refund processed successfully (mock): {reason}",
            ProcessedAt = DateTime.UtcNow,
            Amount = amount
        };

        _logger.LogInformation("Mock refund successful: {TransactionId}", result.TransactionId);

        return Task.FromResult(result);
    }

    public Task<bool> ValidatePaymentMethodAsync(string paymentMethod)
    {
        // Mock validation - accept common payment methods
        var validMethods = new[] { "card", "credit_card", "debit_card", "paypal", "bank_transfer" };
        return Task.FromResult(validMethods.Contains(paymentMethod.ToLowerInvariant()));
    }

    public Task<PaymentStatus> GetPaymentStatusAsync(Guid orderId)
    {
        if (_paymentHistory.TryGetValue(orderId, out var payment))
        {
            return Task.FromResult(new PaymentStatus
            {
                Status = "Completed",
                TransactionId = payment.TransactionId,
                ProcessedAt = payment.ProcessedAt
            });
        }

        return Task.FromResult(new PaymentStatus
        {
            Status = "NotFound",
            TransactionId = null,
            ProcessedAt = null
        });
    }
}

