using UserService.Application.DTOs;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Services;

/// <summary>
/// Service interface for user business logic
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by email
    /// </summary>
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users with pagination
    /// </summary>
    Task<PagedResultDto<UserDto>> GetAllUsersAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches users by criteria
    /// </summary>
    Task<PagedResultDto<UserDto>> SearchUsersAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users by role
    /// </summary>
    Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<UserDto> CreateUserAsync(CreateUserDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user
    /// </summary>
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user (soft delete)
    /// </summary>
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's role (admin action)
    /// </summary>
    Task<UserDto> ChangeUserRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports user data for GDPR compliance
    /// </summary>
    Task<object> ExportUserDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymizes a user for GDPR right to be forgotten
    /// </summary>
    Task<bool> AnonymizeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets role statistics
    /// </summary>
    Task<Dictionary<string, int>> GetRoleStatisticsAsync(CancellationToken cancellationToken = default);
}

