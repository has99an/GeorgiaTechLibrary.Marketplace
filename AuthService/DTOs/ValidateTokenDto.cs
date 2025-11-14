using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs;

public class ValidateTokenDto
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
