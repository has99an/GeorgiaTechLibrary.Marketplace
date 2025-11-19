using MediatR;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Query to get autocomplete suggestions based on prefix
/// </summary>
public record GetAutocompleteQuery(string Prefix, int MaxResults = 10) : IRequest<GetAutocompleteResult>;

/// <summary>
/// Result containing autocomplete suggestions
/// </summary>
public record GetAutocompleteResult(IEnumerable<AutocompleteSuggestion> Suggestions);

/// <summary>
/// Autocomplete suggestion with type and score
/// </summary>
public record AutocompleteSuggestion(
    string Text,
    string Type, // "title", "author", "isbn"
    double Score
);

