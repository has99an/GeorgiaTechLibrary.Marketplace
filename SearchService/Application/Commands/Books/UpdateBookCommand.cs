using MediatR;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Command to update an existing book
/// </summary>
public record UpdateBookCommand(
    string ISBN,
    string BookTitle,
    string BookAuthor,
    int YearOfPublication,
    string Publisher,
    string? ImageUrlS = null,
    string? ImageUrlM = null,
    string? ImageUrlL = null,
    string Genre = "",
    string Language = "English",
    int PageCount = 0,
    string Description = "",
    double Rating = 0.0,
    string AvailabilityStatus = "Available",
    string Edition = "",
    string Format = "Paperback") : IRequest<UpdateBookResult>;

/// <summary>
/// Result of update book command
/// </summary>
public record UpdateBookResult(bool Success, string? ErrorMessage = null);

