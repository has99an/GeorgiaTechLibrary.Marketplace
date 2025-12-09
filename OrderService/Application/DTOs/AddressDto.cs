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

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters")]
    public string? State { get; set; }

    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
    public string? Country { get; set; }
}

/// <summary>
/// Data transfer object for checkout request
/// </summary>
public class CheckoutDto
{
    [Required(ErrorMessage = "Delivery address is required")]
    public AddressDto DeliveryAddress { get; set; } = null!;

    [Required(ErrorMessage = "Payment amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Payment method is required")]
    [StringLength(50, ErrorMessage = "Payment method cannot exceed 50 characters")]
    public string PaymentMethod { get; set; } = string.Empty;
}

