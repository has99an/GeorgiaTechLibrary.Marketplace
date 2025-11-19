using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.Services;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Handler for GetSearchFacetsQuery
/// </summary>
public class GetSearchFacetsQueryHandler : IRequestHandler<GetSearchFacetsQuery, GetSearchFacetsResult>
{
    private readonly IBookRepository _repository;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<GetSearchFacetsQueryHandler> _logger;

    public GetSearchFacetsQueryHandler(
        IBookRepository repository,
        ISearchIndexService searchIndex,
        ILogger<GetSearchFacetsQueryHandler> logger)
    {
        _repository = repository;
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<GetSearchFacetsResult> Handle(GetSearchFacetsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting search facets for term: {SearchTerm}", request.SearchTerm);

        // Get all available books (or filtered by search term)
        var books = await _repository.GetAllAsync(cancellationToken);
        
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // Filter by search term if provided
            var terms = request.SearchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant());
            var isbns = await _searchIndex.SearchByTermsAsync(terms, cancellationToken);
            books = books.Where(b => isbns.Contains(b.Isbn));
        }

        var booksList = books.ToList();

        // Calculate facets
        var genres = booksList
            .Where(b => !string.IsNullOrWhiteSpace(b.Genre))
            .GroupBy(b => b.Genre)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var languages = booksList
            .Where(b => !string.IsNullOrWhiteSpace(b.Language))
            .GroupBy(b => b.Language)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var formats = booksList
            .Where(b => !string.IsNullOrWhiteSpace(b.Format))
            .GroupBy(b => b.Format)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var publishers = booksList
            .Where(b => !string.IsNullOrWhiteSpace(b.Publisher))
            .GroupBy(b => b.Publisher)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(20) // Limit to top 20 publishers
            .ToList();

        var conditions = booksList
            .SelectMany(b => b.AvailableConditions)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .GroupBy(c => c)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var priceRange = new PriceRangeFacet(
            booksList.Any() ? booksList.Min(b => b.Pricing.MinPrice) : 0,
            booksList.Any() ? booksList.Max(b => b.Pricing.MaxPrice) : 0,
            booksList.Any() ? booksList.Average(b => b.Pricing.AveragePrice) : 0
        );

        var ratingFacet = new RatingFacet(
            booksList.Any() ? booksList.Min(b => b.Rating) : 0,
            booksList.Any() ? booksList.Max(b => b.Rating) : 0,
            booksList.Any() ? booksList.Average(b => b.Rating) : 0
        );

        var facets = new SearchFacets(genres, languages, formats, conditions, publishers, priceRange, ratingFacet);

        _logger.LogInformation("Returning facets with {GenreCount} genres, {LanguageCount} languages, {FormatCount} formats",
            genres.Count, languages.Count, formats.Count);

        return new GetSearchFacetsResult(facets);
    }
}

