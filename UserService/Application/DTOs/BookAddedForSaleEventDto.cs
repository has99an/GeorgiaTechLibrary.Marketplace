namespace UserService.Application.DTOs;

/// <summary>
/// Event DTO for BookAddedForSale event
/// </summary>
public class BookAddedForSaleEventDto
{
    public Guid ListingId { get; set; }
    public Guid SellerId { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}



