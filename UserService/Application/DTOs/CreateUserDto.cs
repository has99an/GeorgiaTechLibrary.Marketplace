using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new user
/// </summary>
public class CreateUserDto
{
    [Required(ErrorMessage = "Email is required")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 200 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    [RegularExpression("^(Student|Seller|Admin)$", ErrorMessage = "Role must be Student, Seller, or Admin")]
    public string Role { get; set; } = "Student";
}

