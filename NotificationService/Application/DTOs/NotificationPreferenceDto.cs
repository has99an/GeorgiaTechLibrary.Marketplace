namespace NotificationService.Application.DTOs;

public class NotificationPreferenceDto
{
    public Guid PreferenceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; }
    public bool OrderCreatedEnabled { get; set; }
    public bool OrderPaidEnabled { get; set; }
    public bool OrderShippedEnabled { get; set; }
    public bool OrderDeliveredEnabled { get; set; }
    public bool OrderCancelledEnabled { get; set; }
    public bool OrderRefundedEnabled { get; set; }
    public bool SystemNotificationsEnabled { get; set; }
    public bool MarketingEnabled { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

public class UpdateNotificationPreferenceDto
{
    public bool? EmailEnabled { get; set; }
    public bool? OrderCreatedEnabled { get; set; }
    public bool? OrderPaidEnabled { get; set; }
    public bool? OrderShippedEnabled { get; set; }
    public bool? OrderDeliveredEnabled { get; set; }
    public bool? OrderCancelledEnabled { get; set; }
    public bool? OrderRefundedEnabled { get; set; }
    public bool? SystemNotificationsEnabled { get; set; }
    public bool? MarketingEnabled { get; set; }
}

