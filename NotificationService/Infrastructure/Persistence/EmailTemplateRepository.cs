using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Infrastructure.Persistence;

public class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly AppDbContext _context;

    public EmailTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EmailTemplate?> GetByIdAsync(Guid templateId)
    {
        return await _context.EmailTemplates.FindAsync(templateId);
    }

    public async Task<EmailTemplate?> GetByNameAsync(string templateName)
    {
        return await _context.EmailTemplates
            .FirstOrDefaultAsync(et => et.TemplateName == templateName);
    }

    public async Task<EmailTemplate?> GetByTypeAsync(NotificationType type)
    {
        return await _context.EmailTemplates
            .FirstOrDefaultAsync(et => et.Type == type && et.IsActive);
    }

    public async Task<IEnumerable<EmailTemplate>> GetAllActiveAsync()
    {
        return await _context.EmailTemplates
            .Where(et => et.IsActive)
            .ToListAsync();
    }

    public async Task<EmailTemplate> CreateAsync(EmailTemplate template)
    {
        _context.EmailTemplates.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(EmailTemplate template)
    {
        _context.EmailTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid templateId)
    {
        var template = await GetByIdAsync(templateId);
        if (template != null)
        {
            _context.EmailTemplates.Remove(template);
            await _context.SaveChangesAsync();
        }
    }
}

