using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Models;

[Table("OrderItems")]
public class OrderItem
{
    [Key]
    public Guid OrderItemId { get; set; }

    [Required]
    public Guid OrderId { get; set; }

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

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Shipped

    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;
}
