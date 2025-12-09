using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class CreateOrderDto
{
    [Required]
    [StringLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemDto> OrderItems { get; set; } = new();

    [Required(ErrorMessage = "Delivery address is required")]
    public AddressDto DeliveryAddress { get; set; } = null!;
}

public class CreateOrderItemDto
{
    [Required]
    [StringLength(13, MinimumLength = 10)]
    public string BookISBN { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    [Range(1, 1000)]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, 10000)]
    public decimal UnitPrice { get; set; }
}

