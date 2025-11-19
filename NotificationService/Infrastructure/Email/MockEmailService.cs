using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Email;

public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;
    private readonly Dictionary<string, EmailResult> _sentEmails = new();

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        _logger.LogInformation("Mock: Sending email to {Email} with subject '{Subject}'", toEmail, subject);

        var result = new EmailResult
        {
            Success = true,
            Message = "Email sent successfully (mock)",
            MessageId = $"MOCK-{Guid.NewGuid():N}",
            SentAt = DateTime.UtcNow
        };

        _sentEmails[result.MessageId] = result;

        _logger.LogInformation("Mock: Email sent with ID {MessageId}", result.MessageId);

        return Task.FromResult(result);
    }

    public async Task<EmailResult> SendTemplatedEmailAsync(string toEmail, string templateName, Dictionary<string, string> placeholders)
    {
        _logger.LogInformation("Mock: Sending templated email '{Template}' to {Email}", templateName, toEmail);

        // Simulate template rendering
        var subject = $"[Mock] {templateName}";
        var body = $"Template: {templateName}\nPlaceholders: {string.Join(", ", placeholders.Select(p => $"{p.Key}={p.Value}"))}";

        return await SendEmailAsync(toEmail, subject, body, body);
    }

    public Task<bool> ValidateEmailAsync(string email)
    {
        var isValid = !string.IsNullOrWhiteSpace(email) && email.Contains('@');
        return Task.FromResult(isValid);
    }
}

