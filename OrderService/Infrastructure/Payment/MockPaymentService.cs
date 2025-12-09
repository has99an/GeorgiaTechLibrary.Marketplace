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
        _logger.LogInformation("=== PAYMENT SERVICE: PROCESS PAYMENT STARTED ===");
        _logger.LogInformation("STEP 1: Input parameters - OrderId: {OrderId}, Amount: {Amount}, PaymentMethod: {PaymentMethod}",
            orderId, amount, paymentMethod);

        _logger.LogInformation("STEP 2: Validating payment method...");
        var isValidMethod = paymentMethod != null && !string.IsNullOrWhiteSpace(paymentMethod);
        if (!isValidMethod)
        {
            _logger.LogWarning("STEP 2: Payment method is null or empty");
        }
        else
        {
            _logger.LogInformation("STEP 2: SUCCESS - Payment method validated: {PaymentMethod}", paymentMethod);
        }

        _logger.LogInformation("STEP 3: Validating amount...");
        if (amount <= 0)
        {
            _logger.LogError("STEP 3: FAILED - Invalid amount: {Amount}", amount);
            var failResult = new PaymentResult
            {
                Success = false,
                TransactionId = null,
                Message = $"Invalid payment amount: {amount}",
                ProcessedAt = DateTime.UtcNow,
                Amount = amount
            };
            return Task.FromResult(failResult);
        }
        _logger.LogInformation("STEP 3: SUCCESS - Amount validated: {Amount}", amount);

        _logger.LogInformation("STEP 4: Creating payment result...");
        var result = new PaymentResult
        {
            Success = true,
            TransactionId = $"MOCK-{Guid.NewGuid():N}",
            Message = "Payment processed successfully (mock)",
            ProcessedAt = DateTime.UtcNow,
            Amount = amount
        };

        _logger.LogInformation("STEP 5: Storing payment in history...");
        _paymentHistory[orderId] = result;
        _logger.LogInformation("STEP 5: SUCCESS - Payment stored");

        _logger.LogInformation("=== PAYMENT SERVICE: PROCESS PAYMENT COMPLETED ===");
        _logger.LogInformation("FINAL: Payment successful - TransactionId: {TransactionId}, Amount: {Amount}, ProcessedAt: {ProcessedAt}",
            result.TransactionId, result.Amount, result.ProcessedAt);

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

