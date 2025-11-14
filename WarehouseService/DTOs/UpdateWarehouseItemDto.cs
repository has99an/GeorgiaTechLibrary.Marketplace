using System.ComponentModel.DataAnnotations;

namespace WarehouseService.DTOs;

public class UpdateWarehouseItemDto
{
    [Range(0, int.MaxValue)]
    public int? Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    [StringLength(50)]
    public string? Location { get; set; }

    public bool? IsNew { get; set; } // true = ny bog fra biblioteket, false = brugt fra studerende
}