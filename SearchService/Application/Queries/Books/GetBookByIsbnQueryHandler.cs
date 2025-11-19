using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Handler for GetBookByIsbnQuery
/// </summary>
public class GetBookByIsbnQueryHandler : IRequestHandler<GetBookByIsbnQuery, GetBookByIsbnResult>
{
    private readonly IBookRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetBookByIsbnQueryHandler> _logger;

    public GetBookByIsbnQueryHandler(
        IBookRepository repository,
        IMapper mapper,
        ILogger<GetBookByIsbnQueryHandler> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<GetBookByIsbnResult> Handle(GetBookByIsbnQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting book by ISBN: {Isbn}", request.Isbn);

        var isbn = ISBN.TryCreate(request.Isbn);
        if (isbn == null)
        {
            _logger.LogWarning("Invalid ISBN format: {Isbn}", request.Isbn);
            return new GetBookByIsbnResult(null);
        }

        var book = await _repository.GetByIsbnAsync(isbn, cancellationToken);
        
        if (book == null)
        {
            _logger.LogInformation("Book not found with ISBN: {Isbn}", request.Isbn);
            return new GetBookByIsbnResult(null);
        }

        var bookDto = _mapper.Map<BookDto>(book);

        return new GetBookByIsbnResult(bookDto);
    }
}

