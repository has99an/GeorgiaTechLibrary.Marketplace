using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Commands.Stock;

/// <summary>
/// Handler for UpdateBookStockCommand
/// </summary>
public class UpdateBookStockCommandHandler : IRequestHandler<UpdateBookStockCommand, UpdateBookStockResult>
{
    private readonly IBookRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<UpdateBookStockCommandHandler> _logger;

    public UpdateBookStockCommandHandler(
        IBookRepository repository,
        ICacheService cache,
        ILogger<UpdateBookStockCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UpdateBookStockResult> Handle(UpdateBookStockCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating stock for book ISBN: {ISBN}", request.BookISBN);

            var isbn = ISBN.Create(request.BookISBN);
            var book = await _repository.GetByIsbnAsync(isbn, cancellationToken);

            if (book == null)
            {
                _logger.LogWarning("Book not found with ISBN: {ISBN}", request.BookISBN);
                return new UpdateBookStockResult(false, "Book not found");
            }

            book.UpdateStock(request.TotalStock, request.AvailableSellers, request.MinPrice);
            
            await _repository.AddOrUpdateAsync(book, cancellationToken);
            
            // Clear page caches since stock changed
            await _cache.RemoveByPatternAsync("available:page:*", cancellationToken);

            _logger.LogInformation("Successfully updated stock for book ISBN: {ISBN}", request.BookISBN);

            return new UpdateBookStockResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for book ISBN: {ISBN}", request.BookISBN);
            return new UpdateBookStockResult(false, ex.Message);
        }
    }
}

