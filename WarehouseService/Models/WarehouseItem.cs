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

    [StringLength(50)]
    public string Location { get; set; } = "Main Warehouse"; // eller "Student Seller"

    [Required]
    public bool IsNew { get; set; } // true = ny bog fra biblioteket, false = brugt fra studerende
}
