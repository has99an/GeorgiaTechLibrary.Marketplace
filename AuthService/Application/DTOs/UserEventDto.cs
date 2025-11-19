namespace AuthService.Application.DTOs;

/// <summary>
/// Data transfer object for user events published to message broker
/// </summary>
public class UserEventDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public DateTime CreatedDate { get; set; }
}

