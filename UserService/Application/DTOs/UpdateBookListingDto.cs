using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for updating a book listing
/// </summary>
public class UpdateBookListingDto
{
    [Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10,000")]
    public decimal? Price { get; set; }

    [Range(0, 1000, ErrorMessage = "Quantity must be between 0 and 1,000")]
    public int? Quantity { get; set; }

    [RegularExpression("^(New|Used - Like New|Used - Good|Used - Acceptable|Used)$", 
        ErrorMessage = "Condition must be one of: New, Used - Like New, Used - Good, Used - Acceptable, Used")]
    public string? Condition { get; set; }

    public bool? IsActive { get; set; }
}







