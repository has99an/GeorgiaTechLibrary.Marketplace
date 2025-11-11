using AutoMapper;
using UserService.DTOs;
using UserService.Models;
using UserService.Repositories;
using UserService.Services;
using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IMapper _mapper;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        IMessageProducer messageProducer,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _messageProducer = messageProducer;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        try
        {
            var users = await _userRepository.GetAllUsersAsync();
            var userDtos = _mapper.Map<IEnumerable<UserDto>>(users);
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            var userDto = _mapper.Map<UserDto>(user);
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var emailExists = await _userRepository.EmailExistsAsync(createUserDto.Email);
            if (emailExists)
            {
                return Conflict($"User with email {createUserDto.Email} already exists");
            }

            var user = _mapper.Map<User>(createUserDto);
            // Handle role conversion
            if (Enum.TryParse<UserRole>(createUserDto.Role, true, out var role))
            {
                user.Role = role;
            }
            else
            {
                user.Role = UserRole.Student; // Default
            }
            var createdUser = await _userRepository.AddUserAsync(user);

            // Publish event
            var userCreatedEvent = _mapper.Map<UserEvent>(createdUser);
            _messageProducer.SendMessage(userCreatedEvent, "UserCreated");

            var userDto = _mapper.Map<UserDto>(createdUser);
            return CreatedAtAction(nameof(GetUserById), new { userId = userDto.UserId }, userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid userId, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser = await _userRepository.GetUserByIdAsync(userId);
            if (existingUser == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            // Check if email is being changed and if it already exists
            if (!string.IsNullOrEmpty(updateUserDto.Email) && updateUserDto.Email != existingUser.Email)
            {
                var emailExists = await _userRepository.EmailExistsAsync(updateUserDto.Email);
                if (emailExists)
                {
                    return Conflict($"User with email {updateUserDto.Email} already exists");
                }
            }

            // Apply updates using AutoMapper
            _mapper.Map(updateUserDto, existingUser);

            var updatedUser = await _userRepository.UpdateUserAsync(userId, existingUser);
            if (updatedUser == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            // Publish event
            var userUpdatedEvent = _mapper.Map<UserEvent>(updatedUser);
            _messageProducer.SendMessage(userUpdatedEvent, "UserUpdated");

            var userDto = _mapper.Map<UserDto>(updatedUser);
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            var deleted = await _userRepository.DeleteUserAsync(userId);
            if (!deleted)
            {
                return NotFound($"User with ID {userId} not found");
            }

            // Publish event
            var userDeletedEvent = _mapper.Map<UserEvent>(user);
            _messageProducer.SendMessage(userDeletedEvent, "UserDeleted");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user with ID {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }
}
