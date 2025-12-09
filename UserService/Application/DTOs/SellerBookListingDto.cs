namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for SellerBookListing
/// </summary>
public class SellerBookListingDto
{
    public Guid ListingId { get; set; }
    public Guid SellerId { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsActive { get; set; }
}


