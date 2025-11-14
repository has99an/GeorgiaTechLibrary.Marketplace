using System.ComponentModel.DataAnnotations;

namespace BookService.DTOs;

public class UpdateBookDto
{
    [StringLength(500)]
    public string? BookTitle { get; set; }

    [StringLength(200)]
    public string? BookAuthor { get; set; }

    public int? YearOfPublication { get; set; }

    [StringLength(200)]
    public string? Publisher { get; set; }

    [StringLength(500)]
    public string? ImageUrlS { get; set; }

    [StringLength(500)]
    public string? ImageUrlM { get; set; }

    [StringLength(500)]
    public string? ImageUrlL { get; set; }

    [StringLength(100)]
    public string? Genre { get; set; }

    [StringLength(50)]
    public string? Language { get; set; }

    public int? PageCount { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0.0, 5.0)]
    public double? Rating { get; set; }

    [StringLength(50)]
    public string? AvailabilityStatus { get; set; }

    [StringLength(100)]
    public string? Edition { get; set; }

    [StringLength(50)]
    public string? Format { get; set; }
}