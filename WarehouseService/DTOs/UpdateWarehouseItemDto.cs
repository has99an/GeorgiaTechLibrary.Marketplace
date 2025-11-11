using System.ComponentModel.DataAnnotations;

namespace WarehouseService.DTOs;

public class UpdateWarehouseItemDto
{
    [Range(0, int.MaxValue)]
    public int? Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    [StringLength(50)]
    public string? Condition { get; set; } // "New" or "Used"
}
