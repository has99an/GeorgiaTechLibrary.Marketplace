using NotificationService.Domain.ValueObjects;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Rich domain entity representing an email template
/// </summary>
public class EmailTemplate
{
    public Guid TemplateId { get; private set; }
    public string TemplateName { get; private set; }
    public NotificationType Type { get; private set; }
    public string Subject { get; private set; }
    public string HtmlBody { get; private set; }
    public string TextBody { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }

    // Private constructor for EF Core
    private EmailTemplate()
    {
        TemplateName = string.Empty;
        Subject = string.Empty;
        HtmlBody = string.Empty;
        TextBody = string.Empty;
    }

    private EmailTemplate(
        Guid templateId,
        string templateName,
        NotificationType type,
        string subject,
        string htmlBody,
        string textBody)
    {
        TemplateId = templateId;
        TemplateName = templateName;
        Type = type;
        Subject = subject;
        HtmlBody = htmlBody;
        TextBody = textBody;
        IsActive = true;
        CreatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new email template
    /// </summary>
    public static EmailTemplate Create(
        string templateName,
        NotificationType type,
        string subject,
        string htmlBody,
        string textBody)
    {
        ValidateTemplateName(templateName);
        ValidateSubject(subject);
        ValidateBody(htmlBody, nameof(htmlBody));
        ValidateBody(textBody, nameof(textBody));

        return new EmailTemplate(
            Guid.NewGuid(),
            templateName,
            type,
            subject,
            htmlBody,
            textBody);
    }

    /// <summary>
    /// Updates the template content
    /// </summary>
    public void UpdateContent(string subject, string htmlBody, string textBody)
    {
        ValidateSubject(subject);
        ValidateBody(htmlBody, nameof(htmlBody));
        ValidateBody(textBody, nameof(textBody));

        Subject = subject;
        HtmlBody = htmlBody;
        TextBody = textBody;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the template
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the template
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Renders the template with placeholders replaced
    /// </summary>
    public (string subject, string htmlBody, string textBody) Render(Dictionary<string, string> placeholders)
    {
        var renderedSubject = Subject;
        var renderedHtml = HtmlBody;
        var renderedText = TextBody;

        foreach (var placeholder in placeholders)
        {
            var key = $"{{{{{placeholder.Key}}}}}";
            renderedSubject = renderedSubject.Replace(key, placeholder.Value);
            renderedHtml = renderedHtml.Replace(key, placeholder.Value);
            renderedText = renderedText.Replace(key, placeholder.Value);
        }

        return (renderedSubject, renderedHtml, renderedText);
    }

    private static void ValidateTemplateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Template name cannot exceed 100 characters", nameof(name));
    }

    private static void ValidateSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty", nameof(subject));

        if (subject.Length > 200)
            throw new ArgumentException("Subject cannot exceed 200 characters", nameof(subject));
    }

    private static void ValidateBody(string body, string paramName)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body cannot be empty", paramName);

        if (body.Length > 50000)
            throw new ArgumentException("Body cannot exceed 50000 characters", paramName);
    }
}

