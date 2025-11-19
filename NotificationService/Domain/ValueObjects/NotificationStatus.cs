namespace NotificationService.Domain.ValueObjects;

/// <summary>
/// Enum representing notification status
/// </summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Read = 3
}

/// <summary>
/// Extension methods for NotificationStatus
/// </summary>
public static class NotificationStatusExtensions
{
    public static string ToDisplayString(this NotificationStatus status)
    {
        return status switch
        {
            NotificationStatus.Pending => "Pending",
            NotificationStatus.Sent => "Sent",
            NotificationStatus.Failed => "Failed",
            NotificationStatus.Read => "Read",
            _ => status.ToString()
        };
    }

    public static bool CanTransitionTo(this NotificationStatus currentStatus, NotificationStatus newStatus)
    {
        return currentStatus switch
        {
            NotificationStatus.Pending => newStatus is NotificationStatus.Sent or NotificationStatus.Failed,
            NotificationStatus.Sent => newStatus is NotificationStatus.Read,
            NotificationStatus.Failed => newStatus is NotificationStatus.Sent,
            NotificationStatus.Read => false,
            _ => false
        };
    }
}

