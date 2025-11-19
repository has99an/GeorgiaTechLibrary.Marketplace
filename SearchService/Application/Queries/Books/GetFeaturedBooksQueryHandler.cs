using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Specifications;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Handler for GetFeaturedBooksQuery
/// </summary>
public class GetFeaturedBooksQueryHandler : IRequestHandler<GetFeaturedBooksQuery, GetFeaturedBooksResult>
{
    private readonly IBookRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetFeaturedBooksQueryHandler> _logger;

    public GetFeaturedBooksQueryHandler(
        IBookRepository repository,
        IMapper mapper,
        ILogger<GetFeaturedBooksQueryHandler> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<GetFeaturedBooksResult> Handle(GetFeaturedBooksQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting {Count} featured books", request.Count);

        // Get available books (first 100)
        var spec = new AvailableBooksSpecification(1, 100);
        var books = await _repository.GetAsync(spec, cancellationToken);

        // Randomly select featured books
        var random = new Random();
        var featuredBooks = books
            .OrderBy(x => random.Next())
            .Take(request.Count);

        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(featuredBooks);

        _logger.LogInformation("Returning {Count} featured books", bookDtos.Count());

        return new GetFeaturedBooksResult(bookDtos);
    }
}

