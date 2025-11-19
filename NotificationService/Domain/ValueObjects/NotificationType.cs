namespace NotificationService.Domain.ValueObjects;

/// <summary>
/// Enum representing notification types
/// </summary>
public enum NotificationType
{
    OrderCreated = 0,
    OrderPaid = 1,
    OrderShipped = 2,
    OrderDelivered = 3,
    OrderCancelled = 4,
    OrderRefunded = 5,
    System = 6,
    Marketing = 7
}

/// <summary>
/// Extension methods for NotificationType
/// </summary>
public static class NotificationTypeExtensions
{
    public static string ToDisplayString(this NotificationType type)
    {
        return type switch
        {
            NotificationType.OrderCreated => "Order Created",
            NotificationType.OrderPaid => "Order Paid",
            NotificationType.OrderShipped => "Order Shipped",
            NotificationType.OrderDelivered => "Order Delivered",
            NotificationType.OrderCancelled => "Order Cancelled",
            NotificationType.OrderRefunded => "Order Refunded",
            NotificationType.System => "System Notification",
            NotificationType.Marketing => "Marketing",
            _ => type.ToString()
        };
    }

    public static string GetEmailTemplate(this NotificationType type)
    {
        return type switch
        {
            NotificationType.OrderCreated => "order-created",
            NotificationType.OrderPaid => "order-paid",
            NotificationType.OrderShipped => "order-shipped",
            NotificationType.OrderDelivered => "order-delivered",
            NotificationType.OrderCancelled => "order-cancelled",
            NotificationType.OrderRefunded => "order-refunded",
            NotificationType.System => "system-notification",
            NotificationType.Marketing => "marketing",
            _ => "default"
        };
    }
}

