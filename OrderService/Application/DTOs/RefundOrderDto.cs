using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class RefundOrderDto
{
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

