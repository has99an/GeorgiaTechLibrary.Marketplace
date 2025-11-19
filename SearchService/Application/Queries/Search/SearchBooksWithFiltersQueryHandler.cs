using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Entities;
using SearchService.Domain.Services;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Handler for SearchBooksWithFiltersQuery - advanced multi-facet filtering
/// </summary>
public class SearchBooksWithFiltersQueryHandler : IRequestHandler<SearchBooksWithFiltersQuery, SearchBooksWithFiltersResult>
{
    private readonly IBookRepository _repository;
    private readonly ISearchIndexService _searchIndex;
    private readonly IFacetIndexService _facetIndex;
    private readonly IMapper _mapper;
    private readonly ILogger<SearchBooksWithFiltersQueryHandler> _logger;

    public SearchBooksWithFiltersQueryHandler(
        IBookRepository repository,
        ISearchIndexService searchIndex,
        IFacetIndexService facetIndex,
        IMapper mapper,
        ILogger<SearchBooksWithFiltersQueryHandler> logger)
    {
        _repository = repository;
        _searchIndex = searchIndex;
        _facetIndex = facetIndex;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SearchBooksWithFiltersResult> Handle(SearchBooksWithFiltersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching books with filters: SearchTerm={SearchTerm}, Genres={Genres}, Page={Page}",
            request.SearchTerm, request.Genres?.Count ?? 0, request.Page);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Start with all books or search results
        IEnumerable<Book> books;

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // Search by term first
            var terms = request.SearchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant());
            var searchIsbns = await _searchIndex.SearchByTermsAsync(terms, cancellationToken);
            books = await _repository.GetByIsbnsAsync(searchIsbns, cancellationToken);
        }
        else
        {
            // Get all available books
            books = await _repository.GetAllAsync(cancellationToken);
        }

        // Apply filters
        var filteredBooks = books.AsQueryable();

        if (request.Genres?.Any() == true)
        {
            filteredBooks = filteredBooks.Where(b => request.Genres.Contains(b.Genre));
        }

        if (request.Languages?.Any() == true)
        {
            filteredBooks = filteredBooks.Where(b => request.Languages.Contains(b.Language));
        }

        if (request.Formats?.Any() == true)
        {
            filteredBooks = filteredBooks.Where(b => request.Formats.Contains(b.Format));
        }

        if (request.Publishers?.Any() == true)
        {
            filteredBooks = filteredBooks.Where(b => request.Publishers.Contains(b.Publisher));
        }

        if (request.Conditions?.Any() == true)
        {
            filteredBooks = filteredBooks.Where(b => b.AvailableConditions.Any(c => request.Conditions.Contains(c)));
        }

        if (request.MinPrice.HasValue)
        {
            filteredBooks = filteredBooks.Where(b => b.Pricing.MinPrice >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            filteredBooks = filteredBooks.Where(b => b.Pricing.MinPrice <= request.MaxPrice.Value);
        }

        if (request.MinRating.HasValue)
        {
            filteredBooks = filteredBooks.Where(b => b.Rating >= request.MinRating.Value);
        }

        // Sort
        var sortedBooks = (request.SortBy?.ToLower(), request.SortOrder?.ToLower()) switch
        {
            ("title", "desc") => filteredBooks.OrderByDescending(b => b.Title),
            ("title", _) => filteredBooks.OrderBy(b => b.Title),
            ("price", "desc") => filteredBooks.OrderByDescending(b => b.Pricing.MinPrice),
            ("price", _) => filteredBooks.OrderBy(b => b.Pricing.MinPrice),
            ("rating", "desc") => filteredBooks.OrderByDescending(b => b.Rating),
            ("rating", _) => filteredBooks.OrderBy(b => b.Rating),
            _ => filteredBooks.OrderBy(b => b.Title)
        };

        var totalCount = sortedBooks.Count();

        // Paginate
        var pagedBooks = sortedBooks
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Map to DTOs
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(pagedBooks);
        var pagedResult = new PagedResult<BookDto>(bookDtos, page, pageSize, totalCount);

        // Calculate applied facets (for display)
        var appliedFacets = new SearchFacets(
            Enumerable.Empty<FacetValue>(),
            Enumerable.Empty<FacetValue>(),
            Enumerable.Empty<FacetValue>(),
            Enumerable.Empty<FacetValue>(),
            Enumerable.Empty<FacetValue>(),
            new PriceRangeFacet(0, 0, 0),
            new RatingFacet(0, 0, 0)
        );

        _logger.LogInformation("Found {Total} books with filters, returning page {Page} with {Count} results",
            totalCount, page, bookDtos.Count());

        return new SearchBooksWithFiltersResult(pagedResult, appliedFacets);
    }
}

