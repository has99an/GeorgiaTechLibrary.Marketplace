using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

/// <summary>
/// DTO for confirming payment and completing checkout
/// </summary>
public class ConfirmPaymentDto
{
    [Required]
    public string SessionId { get; set; } = string.Empty;
    
    [Required]
    public string PaymentMethod { get; set; } = string.Empty;
}
