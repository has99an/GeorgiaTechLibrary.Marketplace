namespace OrderService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a payment operation fails
/// </summary>
public class InvalidPaymentException : DomainException
{
    public decimal ExpectedAmount { get; }
    public decimal ActualAmount { get; }

    public InvalidPaymentException(string message) : base(message)
    {
        ExpectedAmount = 0;
        ActualAmount = 0;
    }

    public InvalidPaymentException(decimal expectedAmount, decimal actualAmount) 
        : base($"Payment amount mismatch. Expected: {expectedAmount:C}, Actual: {actualAmount:C}")
    {
        ExpectedAmount = expectedAmount;
        ActualAmount = actualAmount;
    }
}

