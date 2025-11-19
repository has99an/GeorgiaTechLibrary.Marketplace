using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Notification?> GetByIdAsync(Guid notificationId)
    {
        return await _context.Notifications.FindAsync(notificationId);
    }

    public async Task<IEnumerable<Notification>> GetByRecipientIdAsync(string recipientId, int page = 1, int pageSize = 10)
    {
        return await _context.Notifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notification>> GetByStatusAsync(NotificationStatus status, int page = 1, int pageSize = 10)
    {
        return await _context.Notifications
            .Where(n => n.Status == status)
            .OrderByDescending(n => n.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task UpdateAsync(Notification notification)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid notificationId)
    {
        var notification = await GetByIdAsync(notificationId);
        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Notifications.CountAsync();
    }

    public async Task<int> GetRecipientNotificationCountAsync(string recipientId)
    {
        return await _context.Notifications.CountAsync(n => n.RecipientId == recipientId);
    }

    public async Task<int> GetUnreadCountAsync(string recipientId)
    {
        return await _context.Notifications.CountAsync(n => 
            n.RecipientId == recipientId && 
            n.Status != NotificationStatus.Read);
    }
}

