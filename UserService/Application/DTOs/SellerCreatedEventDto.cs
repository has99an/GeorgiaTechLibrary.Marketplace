namespace UserService.Application.DTOs;

/// <summary>
/// Event DTO for SellerCreated event
/// </summary>
public class SellerCreatedEventDto
{
    public Guid SellerId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime CreatedDate { get; set; }
}



