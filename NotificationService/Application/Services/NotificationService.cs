using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Exceptions;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        INotificationPreferenceRepository preferenceRepository,
        IEmailService emailService,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _preferenceRepository = preferenceRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto createDto)
    {
        _logger.LogInformation("Creating notification for recipient {RecipientId}", createDto.RecipientId);

        if (!Enum.TryParse<NotificationType>(createDto.Type, out var notificationType))
            throw new ArgumentException($"Invalid notification type: {createDto.Type}");

        var notification = Notification.Create(
            createDto.RecipientId,
            createDto.RecipientEmail,
            notificationType,
            createDto.Subject,
            createDto.Message,
            createDto.Metadata);

        var created = await _notificationRepository.CreateAsync(notification);

        _logger.LogInformation("Notification {NotificationId} created", created.NotificationId);

        return MapToDto(created);
    }

    public async Task<NotificationDto?> GetNotificationByIdAsync(Guid notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        return notification != null ? MapToDto(notification) : null;
    }

    public async Task<PagedNotificationsDto> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var notifications = await _notificationRepository.GetByRecipientIdAsync(userId, page, pageSize);
        var totalCount = await _notificationRepository.GetRecipientNotificationCountAsync(userId);

        return new PagedNotificationsDto
        {
            Items = notifications.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _notificationRepository.GetUnreadCountAsync(userId);
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification == null)
            throw new NotificationNotFoundException(notificationId);

        notification.MarkAsRead();
        await _notificationRepository.UpdateAsync(notification);

        _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
    }

    public async Task SendNotificationAsync(Guid notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification == null)
            throw new NotificationNotFoundException(notificationId);

        // Check user preferences
        var preferences = await _preferenceRepository.GetOrCreateForUserAsync(notification.RecipientId);
        if (!preferences.IsEnabled(notification.Type))
        {
            _logger.LogInformation("Notification {NotificationId} skipped due to user preferences", notificationId);
            return;
        }

        try
        {
            // Send email
            var result = await _emailService.SendEmailAsync(
                notification.RecipientEmail,
                notification.Subject,
                notification.Message,
                notification.Message);

            if (result.Success)
            {
                notification.MarkAsSent();
                _logger.LogInformation("Notification {NotificationId} sent successfully", notificationId);
            }
            else
            {
                notification.MarkAsFailed(result.Message);
                _logger.LogWarning("Notification {NotificationId} failed: {Message}", notificationId, result.Message);
            }
        }
        catch (Exception ex)
        {
            notification.MarkAsFailed(ex.Message);
            _logger.LogError(ex, "Error sending notification {NotificationId}", notificationId);
        }

        await _notificationRepository.UpdateAsync(notification);
    }

    public async Task RetryFailedNotificationAsync(Guid notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification == null)
            throw new NotificationNotFoundException(notificationId);

        if (!notification.CanRetry())
            throw new InvalidOperationException("Notification cannot be retried");

        notification.ResetForRetry();
        await _notificationRepository.UpdateAsync(notification);

        await SendNotificationAsync(notificationId);
    }

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            NotificationId = notification.NotificationId,
            RecipientId = notification.RecipientId,
            RecipientEmail = notification.RecipientEmail,
            Type = notification.Type.ToString(),
            Subject = notification.Subject,
            Message = notification.Message,
            Status = notification.Status.ToString(),
            CreatedDate = notification.CreatedDate,
            SentDate = notification.SentDate,
            ReadDate = notification.ReadDate,
            ErrorMessage = notification.ErrorMessage,
            RetryCount = notification.RetryCount,
            Metadata = notification.Metadata
        };
    }
}

