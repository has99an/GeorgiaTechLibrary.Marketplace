namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for SellerReview
/// </summary>
public class SellerReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid SellerId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

/// <summary>
/// DTO for creating a seller review
/// </summary>
public class CreateSellerReviewDto
{
    public Guid OrderId { get; set; }
    public Guid SellerId { get; set; }
    public decimal Rating { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// DTO for updating a seller review
/// </summary>
public class UpdateSellerReviewDto
{
    public decimal Rating { get; set; }
    public string? Comment { get; set; }
}






