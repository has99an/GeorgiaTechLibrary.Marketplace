using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class PayOrderDto
{
    [Required]
    [StringLength(100)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
