namespace OrderService.Models;

/// <summary>
/// Event published when an order item's status changes
/// </summary>
public class OrderItemStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
}

