namespace NotificationService.Domain.Exceptions;

/// <summary>
/// Exception thrown when email sending fails
/// </summary>
public class EmailSendException : DomainException
{
    public string RecipientEmail { get; }

    public EmailSendException(string recipientEmail, string message) 
        : base($"Failed to send email to '{recipientEmail}': {message}")
    {
        RecipientEmail = recipientEmail;
    }

    public EmailSendException(string recipientEmail, string message, Exception innerException) 
        : base($"Failed to send email to '{recipientEmail}': {message}", innerException)
    {
        RecipientEmail = recipientEmail;
    }
}

