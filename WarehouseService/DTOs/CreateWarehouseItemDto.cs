using System.ComponentModel.DataAnnotations;

namespace WarehouseService.DTOs;

public class CreateWarehouseItemDto
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
    public decimal Price { get; set; }

    [Required]
    [StringLength(50)]
    public string Condition { get; set; } = string.Empty; // "New" or "Used"
}
