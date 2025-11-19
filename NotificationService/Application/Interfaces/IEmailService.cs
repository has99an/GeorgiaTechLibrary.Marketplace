namespace NotificationService.Application.Interfaces;

public interface IEmailService
{
    Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody);
    Task<EmailResult> SendTemplatedEmailAsync(string toEmail, string templateName, Dictionary<string, string> placeholders);
    Task<bool> ValidateEmailAsync(string email);
}

public class EmailResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public DateTime SentAt { get; set; }
}

