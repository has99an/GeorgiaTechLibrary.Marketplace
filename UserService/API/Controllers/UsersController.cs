using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Domain.ValueObjects;
using ValidationErrorResponse = UserService.Application.DTOs.ValidationErrorResponse;

namespace UserService.API.Controllers;

/// <summary>
/// Controller for user management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all users with pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<UserDto>), 200)]
    public async Task<ActionResult<PagedResultDto<UserDto>>> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _userService.GetAllUsersAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Gets a user by ID
    /// </summary>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { Message = $"User with ID {userId} not found" });
        }

        return Ok(user);
    }

    /// <summary>
    /// Gets the current user from JWT token
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
        {
            return Unauthorized(new { Message = "User ID not found in request" });
        }

        var user = await _userService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { Message = "User not found" });
        }

        return Ok(user);
    }

    /// <summary>
    /// Searches users by criteria
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResultDto<UserDto>), 200)]
    public async Task<ActionResult<PagedResultDto<UserDto>>> SearchUsers([FromQuery] UserSearchDto searchDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _userService.SearchUsersAsync(searchDto);
        return Ok(result);
    }

    /// <summary>
    /// Gets users by role
    /// </summary>
    [HttpGet("role/{role}")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersByRole(string role)
    {
        if (!UserRoleExtensions.IsValidRole(role))
        {
            return BadRequest(new { Message = "Invalid role. Must be Student, Seller, or Admin" });
        }

        var userRole = UserRoleExtensions.ParseRole(role);
        var users = await _userService.GetUsersByRoleAsync(userRole);
        return Ok(users);
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userService.CreateUserAsync(createDto);
        return CreatedAtAction(nameof(GetUser), new { userId = user.UserId }, user);
    }

    /// <summary>
    /// Updates a user
    /// </summary>
    [HttpPut("{userId}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid userId, [FromBody] UpdateUserDto? updateDto)
    {
        if (updateDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            // Build error response manually to avoid serialization issues
            var errors = new Dictionary<string, string[]>();
            foreach (var error in ModelState)
            {
                var errorMessages = error.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>();
                if (errorMessages.Length > 0)
                {
                    errors[error.Key] = errorMessages;
                }
            }
            
            var errorResponse = new ValidationErrorResponse
            {
                StatusCode = 400,
                Title = "Validation Error",
                Detail = "One or more validation errors occurred",
                Errors = errors
            };
            
            return BadRequest(errorResponse);
        }

        var user = await _userService.UpdateUserAsync(userId, updateDto);
        return Ok(user);
    }

    /// <summary>
    /// Deletes a user (soft delete)
    /// </summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        await _userService.DeleteUserAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Changes a user's role (admin only)
    /// </summary>
    [HttpPut("{userId}/role")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDto>> ChangeUserRole(Guid userId, [FromBody] ChangeRoleDto roleDto)
    {
        if (!UserRoleExtensions.IsValidRole(roleDto.Role))
        {
            return BadRequest(new { Message = "Invalid role. Must be Student, Seller, or Admin" });
        }

        var newRole = UserRoleExtensions.ParseRole(roleDto.Role);
        var user = await _userService.ChangeUserRoleAsync(userId, newRole);
        return Ok(user);
    }

    /// <summary>
    /// Exports user data for GDPR compliance
    /// </summary>
    [HttpGet("{userId}/export")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<object>> ExportUserData(Guid userId)
    {
        var data = await _userService.ExportUserDataAsync(userId);
        return Ok(data);
    }

    /// <summary>
    /// Anonymizes a user for GDPR right to be forgotten
    /// </summary>
    [HttpPost("{userId}/anonymize")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AnonymizeUser(Guid userId)
    {
        await _userService.AnonymizeUserAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Gets role statistics
    /// </summary>
    [HttpGet("statistics/roles")]
    [ProducesResponseType(typeof(Dictionary<string, int>), 200)]
    public async Task<ActionResult<Dictionary<string, int>>> GetRoleStatistics()
    {
        var stats = await _userService.GetRoleStatisticsAsync();
        return Ok(stats);
    }
}

/// <summary>
/// DTO for changing user role
/// </summary>
public class ChangeRoleDto
{
    public string Role { get; set; } = string.Empty;
}

