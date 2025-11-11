using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models;

[Table("Users")]
public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.Student;

    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public enum UserRole
{
    Student,
    Seller,
    Admin
}
