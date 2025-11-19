using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Handler for DeleteBookCommand
/// </summary>
public class DeleteBookCommandHandler : IRequestHandler<DeleteBookCommand, DeleteBookResult>
{
    private readonly IBookRepository _repository;
    private readonly ISearchIndexService _searchIndex;
    private readonly ICacheService _cache;
    private readonly ILogger<DeleteBookCommandHandler> _logger;

    public DeleteBookCommandHandler(
        IBookRepository repository,
        ISearchIndexService searchIndex,
        ICacheService cache,
        ILogger<DeleteBookCommandHandler> logger)
    {
        _repository = repository;
        _searchIndex = searchIndex;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DeleteBookResult> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deleting book with ISBN: {ISBN}", request.ISBN);

            var isbn = ISBN.Create(request.ISBN);
            var book = await _repository.GetByIsbnAsync(isbn, cancellationToken);

            if (book != null)
            {
                await _searchIndex.RemoveFromIndexAsync(isbn, book, cancellationToken);
            }

            await _repository.DeleteAsync(isbn, cancellationToken);
            
            // Clear related caches
            await _cache.RemoveAsync($"sellers:{request.ISBN}", cancellationToken);
            await _cache.RemoveByPatternAsync("available:page:*", cancellationToken);

            _logger.LogInformation("Successfully deleted book with ISBN: {ISBN}", request.ISBN);

            return new DeleteBookResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting book with ISBN: {ISBN}", request.ISBN);
            return new DeleteBookResult(false, ex.Message);
        }
    }
}

