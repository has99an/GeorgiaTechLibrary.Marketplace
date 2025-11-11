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
}
