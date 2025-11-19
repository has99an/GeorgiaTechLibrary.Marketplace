using NotificationService.Application.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationService.Infrastructure.Email;

public class SendGridEmailService : IEmailService
{
    private readonly ILogger<SendGridEmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailService(
        ILogger<SendGridEmailService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["Email:SendGrid:ApiKey"] ?? string.Empty;
        _fromEmail = _configuration["Email:SendGrid:FromEmail"] ?? "noreply@georgiatech-marketplace.com";
        _fromName = _configuration["Email:SendGrid:FromName"] ?? "Georgia Tech Marketplace";
    }

    public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        _logger.LogInformation("SendGrid: Sending email to {Email} with subject '{Subject}'", toEmail, subject);

        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("SendGrid API key not configured, falling back to mock");
                return await SimulateMockEmailAsync(toEmail, subject);
            }

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, textBody, htmlBody);

            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.GetValues("X-Message-Id").FirstOrDefault() ?? Guid.NewGuid().ToString();
                
                _logger.LogInformation("SendGrid: Email sent successfully with ID {MessageId}", messageId);

                return new EmailResult
                {
                    Success = true,
                    Message = "Email sent successfully via SendGrid",
                    MessageId = messageId,
                    SentAt = DateTime.UtcNow
                };
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid: Failed to send email. Status: {Status}, Body: {Body}", 
                    response.StatusCode, errorBody);

                return new EmailResult
                {
                    Success = false,
                    Message = $"SendGrid error: {response.StatusCode}",
                    MessageId = null,
                    SentAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid: Error sending email to {Email}", toEmail);
            
            return new EmailResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                MessageId = null,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<EmailResult> SendTemplatedEmailAsync(string toEmail, string templateName, Dictionary<string, string> placeholders)
    {
        _logger.LogInformation("SendGrid: Sending templated email '{Template}' to {Email}", templateName, toEmail);

        // TODO: Implement SendGrid dynamic templates
        // For now, fall back to basic email
        var subject = $"{templateName}";
        var body = $"Template: {templateName}\n{string.Join("\n", placeholders.Select(p => $"{p.Key}: {p.Value}"))}";

        return await SendEmailAsync(toEmail, subject, body, body);
    }

    public Task<bool> ValidateEmailAsync(string email)
    {
        var isValid = !string.IsNullOrWhiteSpace(email) && 
                     email.Contains('@') && 
                     email.Length >= 5 &&
                     email.Length <= 255;
        return Task.FromResult(isValid);
    }

    private Task<EmailResult> SimulateMockEmailAsync(string toEmail, string subject)
    {
        return Task.FromResult(new EmailResult
        {
            Success = true,
            Message = "Email sent successfully (SendGrid mock mode)",
            MessageId = $"SENDGRID-MOCK-{Guid.NewGuid():N}",
            SentAt = DateTime.UtcNow
        });
    }
}

