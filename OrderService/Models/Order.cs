using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Models;

[Table("Orders")]
public class Order
{
    [Key]
    public Guid OrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public DateTime OrderDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Paid, Shipped, Completed

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
