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
        _logger.LogDebug("Created specification: Skip={Skip}, Take={Take}, OrderBy={OrderBy}, OrderByDescending={OrderByDescending}",
            spec.Skip, spec.Take, spec.OrderBy?.ToString() ?? "null", spec.OrderByDescending?.ToString() ?? "null");

        // Get books and count
        _logger.LogDebug("Calling repository.GetAsync() with specification");
        var books = await _repository.GetAsync(spec, cancellationToken);
        var booksList = books.ToList();
        _logger.LogInformation("Repository returned {Count} books", booksList.Count);

        _logger.LogDebug("Calling repository.CountAsync() with specification");
        var totalCount = await _repository.CountAsync(spec, cancellationToken);
        _logger.LogInformation("Repository returned total count: {TotalCount}", totalCount);

        // Map to DTOs
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(booksList);
        var bookDtosList = bookDtos.ToList();

        var pagedResult = new PagedResult<BookDto>(bookDtosList, page, pageSize, totalCount);

        _logger.LogInformation("Returning {Count} books out of {Total} total (Page {Page} of {TotalPages})",
            bookDtosList.Count, totalCount, page, pagedResult.TotalPages);

        if (bookDtosList.Count == 0 && totalCount == 0)
        {
            _logger.LogWarning("No available books found. This may indicate: 1) No books in Redis, 2) No books have stock > 0, 3) Sorted sets are empty. Check /search/debug endpoint for details.");
        }
        else if (bookDtosList.Count == 0 && totalCount > 0)
        {
            _logger.LogWarning("Total count is {TotalCount} but no books returned. This may indicate a pagination issue.", totalCount);
        }

        return new GetAvailableBooksResult(pagedResult);
    }
}

