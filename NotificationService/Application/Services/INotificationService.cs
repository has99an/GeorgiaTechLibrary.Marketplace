using NotificationService.Application.DTOs;

namespace NotificationService.Application.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto createDto);
    Task<NotificationDto?> GetNotificationByIdAsync(Guid notificationId);
    Task<PagedNotificationsDto> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 10);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(Guid notificationId);
    Task SendNotificationAsync(Guid notificationId);
    Task RetryFailedNotificationAsync(Guid notificationId);
}

