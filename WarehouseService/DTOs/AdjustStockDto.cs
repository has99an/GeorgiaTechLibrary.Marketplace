using System.ComponentModel.DataAnnotations;

namespace WarehouseService.DTOs;

public class AdjustStockDto
{
    [Required]
    [StringLength(13)]
    public string BookISBN { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    public int QuantityChange { get; set; } // Positive for increase, negative for decrease
}
