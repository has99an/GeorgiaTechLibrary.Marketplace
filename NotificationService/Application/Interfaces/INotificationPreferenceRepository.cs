using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(string userId);
    Task<NotificationPreference> CreateAsync(NotificationPreference preference);
    Task UpdateAsync(NotificationPreference preference);
    Task<NotificationPreference> GetOrCreateForUserAsync(string userId);
}

