using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Entities;
using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Handler for CreateBookCommand
/// </summary>
public class CreateBookCommandHandler : IRequestHandler<CreateBookCommand, CreateBookResult>
{
    private readonly IBookRepository _repository;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<CreateBookCommandHandler> _logger;

    public CreateBookCommandHandler(
        IBookRepository repository,
        ISearchIndexService searchIndex,
        ILogger<CreateBookCommandHandler> logger)
    {
        _repository = repository;
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<CreateBookResult> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating book with ISBN: {ISBN}", request.ISBN);

            var isbn = ISBN.Create(request.ISBN);
            
            var book = Book.Create(
                isbn,
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
                request.AvailabilityStatus,
                request.Edition,
                request.Format);

            await _repository.AddOrUpdateAsync(book, cancellationToken);
            await _searchIndex.IndexBookAsync(book, cancellationToken);

            _logger.LogInformation("Successfully created book with ISBN: {ISBN}", request.ISBN);

            return new CreateBookResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book with ISBN: {ISBN}", request.ISBN);
            return new CreateBookResult(false, ex.Message);
        }
    }
}

