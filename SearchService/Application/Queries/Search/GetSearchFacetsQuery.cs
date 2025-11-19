using MediatR;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Query to get available facets for filtering
/// </summary>
public record GetSearchFacetsQuery(string? SearchTerm = null) : IRequest<GetSearchFacetsResult>;

/// <summary>
/// Result containing available facets with counts
/// </summary>
public record GetSearchFacetsResult(SearchFacets Facets);

/// <summary>
/// Available facets for filtering
/// </summary>
public record SearchFacets(
    IEnumerable<FacetValue> Genres,
    IEnumerable<FacetValue> Languages,
    IEnumerable<FacetValue> Formats,
    IEnumerable<FacetValue> Conditions,
    IEnumerable<FacetValue> Publishers,
    PriceRangeFacet PriceRange,
    RatingFacet Rating
);

/// <summary>
/// Facet value with count
/// </summary>
public record FacetValue(string Value, int Count);

/// <summary>
/// Price range facet
/// </summary>
public record PriceRangeFacet(decimal MinPrice, decimal MaxPrice, decimal AveragePrice);

/// <summary>
/// Rating facet
/// </summary>
public record RatingFacet(double MinRating, double MaxRating, double AverageRating);

