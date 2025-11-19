using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class PayOrderDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string PaymentMethod { get; set; } = "card";
}

