namespace CompensationService.Models;

/// <summary>
/// Event published when inventory reservation fails after all retries
/// </summary>
public class InventoryReservationFailedEvent
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int RetryAttempts { get; set; }
}

/// <summary>
/// Event published when seller stats update fails after all retries
/// </summary>
public class SellerStatsUpdateFailedEvent
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string BookISBN { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int RetryAttempts { get; set; }
}

/// <summary>
/// Event published when notification sending fails after all retries
/// </summary>
public class NotificationFailedEvent
{
    public Guid OrderId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int RetryAttempts { get; set; }
}

/// <summary>
/// Event published by CompensationOrchestrator when compensation is required
/// </summary>
public class CompensationRequiredEvent
{
    public Guid OrderId { get; set; }
    public List<FailedItem> FailedItems { get; set; } = new();
    public DateTime RequestedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class FailedItem
{
    public Guid OrderItemId { get; set; }
    public string FailureType { get; set; } = string.Empty; // "InventoryReservation", "SellerStatsUpdate", "Notification"
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Event published when compensation is completed
/// </summary>
public class CompensationCompletedEvent
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string CompensationType { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event published when all compensations are complete and order should be cancelled
/// </summary>
public class OrderCancellationRequestedEvent
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public List<FailedItem> FailedItems { get; set; } = new();
}

