using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Specifications;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Handler for GetAvailableBooksQuery
/// </summary>
public class GetAvailableBooksQueryHandler : IRequestHandler<GetAvailableBooksQuery, GetAvailableBooksResult>
{
    private readonly IBookRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetAvailableBooksQueryHandler> _logger;

    public GetAvailableBooksQueryHandler(
        IBookRepository repository,
        IMapper mapper,
        ILogger<GetAvailableBooksQueryHandler> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<GetAvailableBooksResult> Handle(GetAvailableBooksQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting available books: Page {Page}, PageSize {PageSize}, SortBy {SortBy}, SortOrder {SortOrder}",
            request.Page, request.PageSize, request.SortBy, request.SortOrder);

        // Validate pagination parameters
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var descending = request.SortOrder?.ToLower() == "desc";

        // Create specification
        var spec = new AvailableBooksSpecification(page, pageSize, request.SortBy, descending);

        // Get books and count
        var books = await _repository.GetAsync(spec, cancellationToken);
        var totalCount = await _repository.CountAsync(spec, cancellationToken);

        // Map to DTOs
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(books);

        var pagedResult = new PagedResult<BookDto>(bookDtos, page, pageSize, totalCount);

        _logger.LogInformation("Returning {Count} books out of {Total} total", bookDtos.Count(), totalCount);

        return new GetAvailableBooksResult(pagedResult);
    }
}

