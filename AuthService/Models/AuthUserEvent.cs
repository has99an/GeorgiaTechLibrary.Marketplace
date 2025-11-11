namespace AuthService.Models;

public class AuthUserEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
