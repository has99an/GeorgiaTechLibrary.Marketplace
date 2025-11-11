using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

public class UpdateUserDto
{
    [StringLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    public string? Role { get; set; }
}
