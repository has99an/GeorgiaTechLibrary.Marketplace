using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseService.Models;

[Table("WarehouseItems")]
public class WarehouseItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(13)]
    public string BookISBN { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    [StringLength(50)]
    public string Condition { get; set; } = string.Empty; // "New" or "Used"
}
