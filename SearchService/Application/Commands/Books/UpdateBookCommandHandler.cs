using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Handler for UpdateBookCommand
/// </summary>
public class UpdateBookCommandHandler : IRequestHandler<UpdateBookCommand, UpdateBookResult>
{
    private readonly IBookRepository _repository;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<UpdateBookCommandHandler> _logger;

    public UpdateBookCommandHandler(
        IBookRepository repository,
        ISearchIndexService searchIndex,
        ILogger<UpdateBookCommandHandler> logger)
    {
        _repository = repository;
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<UpdateBookResult> Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating book with ISBN: {ISBN}", request.ISBN);

            var isbn = ISBN.Create(request.ISBN);
            var existingBook = await _repository.GetByIsbnAsync(isbn, cancellationToken);

            if (existingBook == null)
            {
                _logger.LogWarning("Book not found with ISBN: {ISBN}", request.ISBN);
                return new UpdateBookResult(false, "Book not found");
            }

            // Update metadata
            existingBook.UpdateMetadata(
                request.BookTitle,
                request.BookAuthor,
                request.YearOfPublication,
                request.Publisher,
                request.ImageUrlS,
                request.ImageUrlM,
                request.ImageUrlL,
                request.Genre,
                request.Language,
                request.PageCount,
                request.Description,
                request.Rating,
                request.Edition,
                request.Format);

            await _repository.AddOrUpdateAsync(existingBook, cancellationToken);
            await _searchIndex.UpdateIndexAsync(existingBook, existingBook, cancellationToken);

            _logger.LogInformation("Successfully updated book with ISBN: {ISBN}", request.ISBN);

            return new UpdateBookResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating book with ISBN: {ISBN}", request.ISBN);
            return new UpdateBookResult(false, ex.Message);
        }
    }
}

