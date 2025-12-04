using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

/// <summary>
/// Data transfer object for Address
/// </summary>
public class AddressDto
{
    [Required(ErrorMessage = "Street is required")]
    [StringLength(200, ErrorMessage = "Street cannot exceed 200 characters")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must be 4 digits")]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
    public string? Country { get; set; }
}

