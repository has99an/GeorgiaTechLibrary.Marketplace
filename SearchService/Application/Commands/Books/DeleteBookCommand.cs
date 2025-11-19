using MediatR;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Command to delete a book
/// </summary>
public record DeleteBookCommand(string ISBN) : IRequest<DeleteBookResult>;

/// <summary>
/// Result of delete book command
/// </summary>
public record DeleteBookResult(bool Success, string? ErrorMessage = null);

