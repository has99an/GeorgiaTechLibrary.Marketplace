using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for updating a user
/// </summary>
public class UpdateUserDto
{
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }

    [StringLength(200, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 200 characters")]
    public string? Name { get; set; }

    [RegularExpression("^(Student|Seller|Admin)$", ErrorMessage = "Role must be Student, Seller, or Admin")]
    public string? Role { get; set; }

    public AddressDto? DeliveryAddress { get; set; }
}

