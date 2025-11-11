using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

public class CreateUserDto
{
    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Student"; // Default to Student
}
