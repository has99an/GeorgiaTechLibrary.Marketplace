namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for User responses
/// </summary>
public class UserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public AddressDto? DeliveryAddress { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

