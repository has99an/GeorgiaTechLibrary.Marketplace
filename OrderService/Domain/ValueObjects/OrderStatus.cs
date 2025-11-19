namespace OrderService.Domain.ValueObjects;

/// <summary>
/// Enum representing order status with valid transitions
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
    Refunded = 5
}

/// <summary>
/// Extension methods for OrderStatus
/// </summary>
public static class OrderStatusExtensions
{
    /// <summary>
    /// Checks if a status transition is valid
    /// </summary>
    public static bool CanTransitionTo(this OrderStatus currentStatus, OrderStatus newStatus)
    {
        return currentStatus switch
        {
            OrderStatus.Pending => newStatus is OrderStatus.Paid or OrderStatus.Cancelled,
            OrderStatus.Paid => newStatus is OrderStatus.Shipped or OrderStatus.Refunded or OrderStatus.Cancelled,
            OrderStatus.Shipped => newStatus is OrderStatus.Delivered,
            OrderStatus.Delivered => newStatus is OrderStatus.Refunded,
            OrderStatus.Cancelled => false, // Terminal state
            OrderStatus.Refunded => false,  // Terminal state
            _ => false
        };
    }

    /// <summary>
    /// Gets the display name for the status
    /// </summary>
    public static string ToDisplayString(this OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => "Pending Payment",
            OrderStatus.Paid => "Paid",
            OrderStatus.Shipped => "Shipped",
            OrderStatus.Delivered => "Delivered",
            OrderStatus.Cancelled => "Cancelled",
            OrderStatus.Refunded => "Refunded",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Checks if the order is in a terminal state
    /// </summary>
    public static bool IsTerminal(this OrderStatus status)
    {
        return status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Delivered;
    }

    /// <summary>
    /// Checks if the order can be cancelled
    /// </summary>
    public static bool CanBeCancelled(this OrderStatus status)
    {
        return status is OrderStatus.Pending or OrderStatus.Paid;
    }

    /// <summary>
    /// Checks if the order can be refunded
    /// </summary>
    public static bool CanBeRefunded(this OrderStatus status)
    {
        return status is OrderStatus.Paid or OrderStatus.Delivered;
    }
}

