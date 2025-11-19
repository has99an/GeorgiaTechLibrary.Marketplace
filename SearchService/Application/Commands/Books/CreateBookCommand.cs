using MediatR;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Command to create a new book
/// </summary>
public record CreateBookCommand(
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
    string Format = "Paperback") : IRequest<CreateBookResult>;

/// <summary>
/// Result of create book command
/// </summary>
public record CreateBookResult(bool Success, string? ErrorMessage = null);

