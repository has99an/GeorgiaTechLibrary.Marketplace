using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

/// <summary>
/// Repository interface for AuthUser entity operations
/// </summary>
public interface IAuthUserRepository
{
    /// <summary>
    /// Gets an auth user by their unique identifier
    /// </summary>
    Task<AuthUser?> GetAuthUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an auth user by their email address
    /// </summary>
    Task<AuthUser?> GetAuthUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new auth user
    /// </summary>
    Task<AuthUser> AddAuthUserAsync(AuthUser authUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing auth user
    /// </summary>
    Task<AuthUser> UpdateAuthUserAsync(AuthUser authUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an auth user exists by ID
    /// </summary>
    Task<bool> AuthUserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an email is already in use
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

