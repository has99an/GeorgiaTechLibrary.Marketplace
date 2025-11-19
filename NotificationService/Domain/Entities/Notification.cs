using NotificationService.Domain.ValueObjects;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a notification
/// </summary>
public class Notification
{
    public Guid NotificationId { get; private set; }
    public string RecipientId { get; private set; }
    public string RecipientEmail { get; private set; }
    public NotificationType Type { get; private set; }
    public string Subject { get; private set; }
    public string Message { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? SentDate { get; private set; }
    public DateTime? ReadDate { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; }

    // Private constructor for EF Core
    private Notification()
    {
        RecipientId = string.Empty;
        RecipientEmail = string.Empty;
        Subject = string.Empty;
        Message = string.Empty;
        Metadata = new Dictionary<string, string>();
    }

    private Notification(
        Guid notificationId,
        string recipientId,
        string recipientEmail,
        NotificationType type,
        string subject,
        string message,
        Dictionary<string, string>? metadata = null)
    {
        NotificationId = notificationId;
        RecipientId = recipientId;
        RecipientEmail = recipientEmail;
        Type = type;
        Subject = subject;
        Message = message;
        Status = NotificationStatus.Pending;
        CreatedDate = DateTime.UtcNow;
        RetryCount = 0;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Factory method to create a new notification
    /// </summary>
    public static Notification Create(
        string recipientId,
        string recipientEmail,
        NotificationType type,
        string subject,
        string message,
        Dictionary<string, string>? metadata = null)
    {
        ValidateRecipientId(recipientId);
        ValidateRecipientEmail(recipientEmail);
        ValidateSubject(subject);
        ValidateMessage(message);

        return new Notification(
            Guid.NewGuid(),
            recipientId,
            recipientEmail,
            type,
            subject,
            message,
            metadata);
    }

    /// <summary>
    /// Marks the notification as sent
    /// </summary>
    public void MarkAsSent()
    {
        if (!Status.CanTransitionTo(NotificationStatus.Sent))
            throw new InvalidOperationException($"Cannot mark notification as sent from status {Status}");

        Status = NotificationStatus.Sent;
        SentDate = DateTime.UtcNow;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the notification as failed
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        if (!Status.CanTransitionTo(NotificationStatus.Failed))
            throw new InvalidOperationException($"Cannot mark notification as failed from status {Status}");

        Status = NotificationStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
    }

    /// <summary>
    /// Marks the notification as read
    /// </summary>
    public void MarkAsRead()
    {
        if (!Status.CanTransitionTo(NotificationStatus.Read))
            throw new InvalidOperationException($"Cannot mark notification as read from status {Status}");

        Status = NotificationStatus.Read;
        ReadDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the notification can be retried
    /// </summary>
    public bool CanRetry(int maxRetries = 3)
    {
        return Status == NotificationStatus.Failed && RetryCount < maxRetries;
    }

    /// <summary>
    /// Resets the notification for retry
    /// </summary>
    public void ResetForRetry()
    {
        if (!CanRetry())
            throw new InvalidOperationException("Notification cannot be retried");

        Status = NotificationStatus.Pending;
        ErrorMessage = null;
    }

    /// <summary>
    /// Adds metadata to the notification
    /// </summary>
    public void AddMetadata(string key, string value)
    {
        Metadata[key] = value;
    }

    private static void ValidateRecipientId(string recipientId)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            throw new ArgumentException("Recipient ID cannot be empty", nameof(recipientId));

        if (recipientId.Length > 100)
            throw new ArgumentException("Recipient ID cannot exceed 100 characters", nameof(recipientId));
    }

    private static void ValidateRecipientEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Recipient email cannot be empty", nameof(email));

        if (!email.Contains('@'))
            throw new ArgumentException("Invalid email format", nameof(email));

        if (email.Length > 255)
            throw new ArgumentException("Email cannot exceed 255 characters", nameof(email));
    }

    private static void ValidateSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty", nameof(subject));

        if (subject.Length > 200)
            throw new ArgumentException("Subject cannot exceed 200 characters", nameof(subject));
    }

    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        if (message.Length > 5000)
            throw new ArgumentException("Message cannot exceed 5000 characters", nameof(message));
    }
}

