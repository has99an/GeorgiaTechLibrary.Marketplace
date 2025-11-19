using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class UpdateCartItemDto
{
    [Required]
    [Range(1, 1000)]
    public int Quantity { get; set; }
}

