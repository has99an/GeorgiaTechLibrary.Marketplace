using NotificationService.Application.DTOs;

namespace NotificationService.Application.Services;

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto> GetUserPreferencesAsync(string userId);
    Task<NotificationPreferenceDto> UpdateUserPreferencesAsync(string userId, UpdateNotificationPreferenceDto updateDto);
    Task DisableAllNotificationsAsync(string userId);
    Task EnableAllNotificationsAsync(string userId);
}

