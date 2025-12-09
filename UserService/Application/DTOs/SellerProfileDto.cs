namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for SellerProfile
/// </summary>
public class SellerProfileDto
{
    public Guid SellerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int TotalSales { get; set; }
    public int TotalBooksSold { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

