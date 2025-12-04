using OrderService.Application.DTOs;

namespace OrderService.Application.Interfaces;

/// <summary>
/// Client interface for communicating with UserService
/// </summary>
public interface IUserServiceClient
{
    /// <summary>
    /// Gets a user by ID from UserService
    /// </summary>
    Task<UserServiceUserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for User from UserService (to avoid namespace conflicts)
/// </summary>
public class UserServiceUserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public AddressDto? DeliveryAddress { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

