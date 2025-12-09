using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

/// <summary>
/// Data transfer object for adding a book for sale
/// </summary>
public class AddBookForSaleDto
{
    [Required(ErrorMessage = "Book ISBN is required")]
    [StringLength(13, MinimumLength = 13, ErrorMessage = "Book ISBN must be exactly 13 characters")]
    [RegularExpression(@"^\d{13}$", ErrorMessage = "Book ISBN must contain only digits")]
    public string BookISBN { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10,000")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(0, 1000, ErrorMessage = "Quantity must be between 0 and 1,000")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Condition is required")]
    [RegularExpression("^(New|Used - Like New|Used - Good|Used - Acceptable|Used)$", 
        ErrorMessage = "Condition must be one of: New, Used - Like New, Used - Good, Used - Acceptable, Used")]
    public string Condition { get; set; } = string.Empty;
}

