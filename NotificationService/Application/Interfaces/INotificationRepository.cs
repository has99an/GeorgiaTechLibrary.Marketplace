using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid notificationId);
    Task<IEnumerable<Notification>> GetByRecipientIdAsync(string recipientId, int page = 1, int pageSize = 10);
    Task<IEnumerable<Notification>> GetByStatusAsync(NotificationStatus status, int page = 1, int pageSize = 10);
    Task<Notification> CreateAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task DeleteAsync(Guid notificationId);
    Task<int> GetTotalCountAsync();
    Task<int> GetRecipientNotificationCountAsync(string recipientId);
    Task<int> GetUnreadCountAsync(string recipientId);
}

