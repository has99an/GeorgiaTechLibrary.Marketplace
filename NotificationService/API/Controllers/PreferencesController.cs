using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications/[controller]")]
public class PreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _preferenceService;
    private readonly ILogger<PreferencesController> _logger;

    public PreferencesController(
        INotificationPreferenceService preferenceService,
        ILogger<PreferencesController> logger)
    {
        _preferenceService = preferenceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets notification preferences for a user
    /// </summary>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferenceDto>> GetPreferences(string userId)
    {
        var preferences = await _preferenceService.GetUserPreferencesAsync(userId);
        return Ok(preferences);
    }

    /// <summary>
    /// Updates notification preferences for a user
    /// </summary>
    [HttpPut("{userId}")]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationPreferenceDto>> UpdatePreferences(
        string userId,
        [FromBody] UpdateNotificationPreferenceDto updateDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var preferences = await _preferenceService.UpdateUserPreferencesAsync(userId, updateDto);
        return Ok(preferences);
    }

    /// <summary>
    /// Disables all notifications for a user
    /// </summary>
    [HttpPost("{userId}/disable-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisableAll(string userId)
    {
        await _preferenceService.DisableAllNotificationsAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Enables all notifications for a user
    /// </summary>
    [HttpPost("{userId}/enable-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EnableAll(string userId)
    {
        await _preferenceService.EnableAllNotificationsAsync(userId);
        return NoContent();
    }
}

