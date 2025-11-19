using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for searching users
/// </summary>
public class UserSearchDto
{
    [StringLength(200, ErrorMessage = "Search term cannot exceed 200 characters")]
    public string? SearchTerm { get; set; }

    [RegularExpression("^(Student|Seller|Admin)$", ErrorMessage = "Role must be Student, Seller, or Admin")]
    public string? Role { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int PageSize { get; set; } = 20;
}

