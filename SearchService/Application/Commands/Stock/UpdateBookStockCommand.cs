using MediatR;

namespace SearchService.Application.Commands.Stock;

/// <summary>
/// Command to update book stock information
/// </summary>
public record UpdateBookStockCommand(
    string BookISBN,
    int TotalStock,
    int AvailableSellers,
    decimal MinPrice) : IRequest<UpdateBookStockResult>;

/// <summary>
/// Result of update book stock command
/// </summary>
public record UpdateBookStockResult(bool Success, string? ErrorMessage = null);

