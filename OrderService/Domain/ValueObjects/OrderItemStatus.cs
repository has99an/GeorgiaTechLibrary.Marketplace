namespace OrderService.Domain.ValueObjects;

/// <summary>
/// Status of an order item in the fulfillment process
/// </summary>
public enum OrderItemStatus
{
    /// <summary>
    /// Item is pending processing
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Item is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Item has been successfully fulfilled
    /// </summary>
    Fulfilled = 2,

    /// <summary>
    /// Item processing failed after all retries
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Item has been compensated (rolled back)
    /// </summary>
    Compensated = 4
}

