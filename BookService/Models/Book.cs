using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookService.Models;

[Table("Books")]
public class Book
{
    [Key]
    [StringLength(13)]
    public string ISBN { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string BookTitle { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string BookAuthor { get; set; } = string.Empty;

    [Required]
    public int YearOfPublication { get; set; }

    [Required]
    [StringLength(200)]
    public string Publisher { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ImageUrlS { get; set; }

    [StringLength(500)]
    public string? ImageUrlM { get; set; }

    [StringLength(500)]
    public string? ImageUrlL { get; set; }

    // 👇 DE 8 NYE FELTER
    [Required]
    [StringLength(100)]
    public string Genre { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Language { get; set; } = "English";

    [Required]
    public int PageCount { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.0, 5.0)]
    public double Rating { get; set; }

    [Required]
    [StringLength(50)]
    public string AvailabilityStatus { get; set; } = "Available";

    [Required]
    [StringLength(100)]
    public string Edition { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Format { get; set; } = "Paperback";
}