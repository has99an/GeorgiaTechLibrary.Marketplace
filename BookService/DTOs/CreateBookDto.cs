using System.ComponentModel.DataAnnotations;

namespace BookService.DTOs;

public class CreateBookDto
{
    [Required]
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
}
