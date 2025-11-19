using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Application.Services;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly ILogger<NotificationPreferenceService> _logger;

    public NotificationPreferenceService(
        INotificationPreferenceRepository preferenceRepository,
        ILogger<NotificationPreferenceService> logger)
    {
        _preferenceRepository = preferenceRepository;
        _logger = logger;
    }

    public async Task<NotificationPreferenceDto> GetUserPreferencesAsync(string userId)
    {
        var preferences = await _preferenceRepository.GetOrCreateForUserAsync(userId);
        return MapToDto(preferences);
    }

    public async Task<NotificationPreferenceDto> UpdateUserPreferencesAsync(string userId, UpdateNotificationPreferenceDto updateDto)
    {
        _logger.LogInformation("Updating notification preferences for user {UserId}", userId);

        var preferences = await _preferenceRepository.GetOrCreateForUserAsync(userId);

        if (updateDto.EmailEnabled.HasValue)
            preferences.SetEmailEnabled(updateDto.EmailEnabled.Value);

        if (updateDto.OrderCreatedEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderCreated, updateDto.OrderCreatedEnabled.Value);

        if (updateDto.OrderPaidEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderPaid, updateDto.OrderPaidEnabled.Value);

        if (updateDto.OrderShippedEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderShipped, updateDto.OrderShippedEnabled.Value);

        if (updateDto.OrderDeliveredEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderDelivered, updateDto.OrderDeliveredEnabled.Value);

        if (updateDto.OrderCancelledEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderCancelled, updateDto.OrderCancelledEnabled.Value);

        if (updateDto.OrderRefundedEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.OrderRefunded, updateDto.OrderRefundedEnabled.Value);

        if (updateDto.SystemNotificationsEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.System, updateDto.SystemNotificationsEnabled.Value);

        if (updateDto.MarketingEnabled.HasValue)
            preferences.UpdatePreference(NotificationType.Marketing, updateDto.MarketingEnabled.Value);

        await _preferenceRepository.UpdateAsync(preferences);

        _logger.LogInformation("Notification preferences updated for user {UserId}", userId);

        return MapToDto(preferences);
    }

    public async Task DisableAllNotificationsAsync(string userId)
    {
        var preferences = await _preferenceRepository.GetOrCreateForUserAsync(userId);
        preferences.DisableAll();
        await _preferenceRepository.UpdateAsync(preferences);

        _logger.LogInformation("All notifications disabled for user {UserId}", userId);
    }

    public async Task EnableAllNotificationsAsync(string userId)
    {
        var preferences = await _preferenceRepository.GetOrCreateForUserAsync(userId);
        preferences.EnableAll();
        await _preferenceRepository.UpdateAsync(preferences);

        _logger.LogInformation("All notifications enabled for user {UserId}", userId);
    }

    private NotificationPreferenceDto MapToDto(Domain.Entities.NotificationPreference preference)
    {
        return new NotificationPreferenceDto
        {
            PreferenceId = preference.PreferenceId,
            UserId = preference.UserId,
            EmailEnabled = preference.EmailEnabled,
            OrderCreatedEnabled = preference.OrderCreatedEnabled,
            OrderPaidEnabled = preference.OrderPaidEnabled,
            OrderShippedEnabled = preference.OrderShippedEnabled,
            OrderDeliveredEnabled = preference.OrderDeliveredEnabled,
            OrderCancelledEnabled = preference.OrderCancelledEnabled,
            OrderRefundedEnabled = preference.OrderRefundedEnabled,
            SystemNotificationsEnabled = preference.SystemNotificationsEnabled,
            MarketingEnabled = preference.MarketingEnabled,
            CreatedDate = preference.CreatedDate,
            UpdatedDate = preference.UpdatedDate
        };
    }
}

