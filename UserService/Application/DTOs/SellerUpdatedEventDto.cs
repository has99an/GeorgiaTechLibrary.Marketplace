namespace UserService.Application.DTOs;

/// <summary>
/// Event DTO for SellerUpdated event
/// </summary>
public class SellerUpdatedEventDto
{
    public Guid SellerId { get; set; }
    public decimal Rating { get; set; }
    public int TotalSales { get; set; }
    public int TotalBooksSold { get; set; }
    public string? Location { get; set; }
    public DateTime UpdatedDate { get; set; }
}




