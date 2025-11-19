using NotificationService.Domain.ValueObjects;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Rich domain entity representing user notification preferences
/// </summary>
public class NotificationPreference
{
    public Guid PreferenceId { get; private set; }
    public string UserId { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool OrderCreatedEnabled { get; private set; }
    public bool OrderPaidEnabled { get; private set; }
    public bool OrderShippedEnabled { get; private set; }
    public bool OrderDeliveredEnabled { get; private set; }
    public bool OrderCancelledEnabled { get; private set; }
    public bool OrderRefundedEnabled { get; private set; }
    public bool SystemNotificationsEnabled { get; private set; }
    public bool MarketingEnabled { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }

    // Private constructor for EF Core
    private NotificationPreference()
    {
        UserId = string.Empty;
    }

    private NotificationPreference(Guid preferenceId, string userId)
    {
        PreferenceId = preferenceId;
        UserId = userId;
        EmailEnabled = true;
        OrderCreatedEnabled = true;
        OrderPaidEnabled = true;
        OrderShippedEnabled = true;
        OrderDeliveredEnabled = true;
        OrderCancelledEnabled = true;
        OrderRefundedEnabled = true;
        SystemNotificationsEnabled = true;
        MarketingEnabled = false;
        CreatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create default preferences for a user
    /// </summary>
    public static NotificationPreference CreateDefault(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        return new NotificationPreference(Guid.NewGuid(), userId);
    }

    /// <summary>
    /// Updates email enabled status
    /// </summary>
    public void SetEmailEnabled(bool enabled)
    {
        EmailEnabled = enabled;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates notification type preferences
    /// </summary>
    public void UpdatePreference(NotificationType type, bool enabled)
    {
        switch (type)
        {
            case NotificationType.OrderCreated:
                OrderCreatedEnabled = enabled;
                break;
            case NotificationType.OrderPaid:
                OrderPaidEnabled = enabled;
                break;
            case NotificationType.OrderShipped:
                OrderShippedEnabled = enabled;
                break;
            case NotificationType.OrderDelivered:
                OrderDeliveredEnabled = enabled;
                break;
            case NotificationType.OrderCancelled:
                OrderCancelledEnabled = enabled;
                break;
            case NotificationType.OrderRefunded:
                OrderRefundedEnabled = enabled;
                break;
            case NotificationType.System:
                SystemNotificationsEnabled = enabled;
                break;
            case NotificationType.Marketing:
                MarketingEnabled = enabled;
                break;
        }

        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if a notification type is enabled
    /// </summary>
    public bool IsEnabled(NotificationType type)
    {
        if (!EmailEnabled)
            return false;

        return type switch
        {
            NotificationType.OrderCreated => OrderCreatedEnabled,
            NotificationType.OrderPaid => OrderPaidEnabled,
            NotificationType.OrderShipped => OrderShippedEnabled,
            NotificationType.OrderDelivered => OrderDeliveredEnabled,
            NotificationType.OrderCancelled => OrderCancelledEnabled,
            NotificationType.OrderRefunded => OrderRefundedEnabled,
            NotificationType.System => SystemNotificationsEnabled,
            NotificationType.Marketing => MarketingEnabled,
            _ => false
        };
    }

    /// <summary>
    /// Disables all notifications
    /// </summary>
    public void DisableAll()
    {
        EmailEnabled = false;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Enables all notifications
    /// </summary>
    public void EnableAll()
    {
        EmailEnabled = true;
        OrderCreatedEnabled = true;
        OrderPaidEnabled = true;
        OrderShippedEnabled = true;
        OrderDeliveredEnabled = true;
        OrderCancelledEnabled = true;
        OrderRefundedEnabled = true;
        SystemNotificationsEnabled = true;
        UpdatedDate = DateTime.UtcNow;
    }
}

