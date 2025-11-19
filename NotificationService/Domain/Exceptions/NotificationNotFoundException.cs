namespace NotificationService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a notification is not found
/// </summary>
public class NotificationNotFoundException : DomainException
{
    public Guid NotificationId { get; }

    public NotificationNotFoundException(Guid notificationId) 
        : base($"Notification with ID '{notificationId}' was not found.")
    {
        NotificationId = notificationId;
    }
}

