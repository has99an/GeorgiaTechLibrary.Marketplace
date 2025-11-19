using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a notification by ID
    /// </summary>
    [HttpGet("{notificationId}")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> GetNotification(Guid notificationId)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(notificationId);
        
        if (notification == null)
            return NotFound();

        return Ok(notification);
    }

    /// <summary>
    /// Gets all notifications for a user (paginated)
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(PagedNotificationsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedNotificationsDto>> GetUserNotifications(
        string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);
        return Ok(notifications);
    }

    /// <summary>
    /// Gets unread notification count for a user
    /// </summary>
    [HttpGet("user/{userId}/unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetUnreadCount(string userId)
    {
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    /// <summary>
    /// Creates a new notification
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] CreateNotificationDto createDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var notification = await _notificationService.CreateNotificationAsync(createDto);
        return CreatedAtAction(nameof(GetNotification), new { notificationId = notification.NotificationId }, notification);
    }

    /// <summary>
    /// Marks a notification as read
    /// </summary>
    [HttpPost("{notificationId}/mark-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notificationService.MarkAsReadAsync(notificationId);
        return NoContent();
    }

    /// <summary>
    /// Sends a notification
    /// </summary>
    [HttpPost("{notificationId}/send")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendNotification(Guid notificationId)
    {
        await _notificationService.SendNotificationAsync(notificationId);
        return NoContent();
    }

    /// <summary>
    /// Retries a failed notification
    /// </summary>
    [HttpPost("{notificationId}/retry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryNotification(Guid notificationId)
    {
        await _notificationService.RetryFailedNotificationAsync(notificationId);
        return NoContent();
    }
}

