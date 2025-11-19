using AuthService.Application.DTOs;
using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

/// <summary>
/// Service interface for JWT token operations
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates access and refresh tokens for an authenticated user
    /// </summary>
    TokenDto GenerateTokens(AuthUser authUser, string? role = null);

    /// <summary>
    /// Validates a JWT token
    /// </summary>
    bool ValidateToken(string token);

    /// <summary>
    /// Extracts the user ID from a JWT token
    /// </summary>
    Guid? ExtractUserIdFromToken(string token);

    /// <summary>
    /// Extracts the email from a JWT token
    /// </summary>
    string? ExtractEmailFromToken(string token);
}

