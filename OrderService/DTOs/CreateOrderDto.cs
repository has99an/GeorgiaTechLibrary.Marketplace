using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class CreateOrderDto
{
    [Required]
    [StringLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemDto> OrderItems { get; set; } = new List<CreateOrderItemDto>();
}

public class CreateOrderItemDto
{
    [Required]
    [StringLength(13)]
    public string BookISBN { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}
