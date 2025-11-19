using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Application.Interfaces;

public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid templateId);
    Task<EmailTemplate?> GetByNameAsync(string templateName);
    Task<EmailTemplate?> GetByTypeAsync(NotificationType type);
    Task<IEnumerable<EmailTemplate>> GetAllActiveAsync();
    Task<EmailTemplate> CreateAsync(EmailTemplate template);
    Task UpdateAsync(EmailTemplate template);
    Task DeleteAsync(Guid templateId);
}

