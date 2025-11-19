using MediatR;
using SearchService.Domain.Services;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Handler for GetAutocompleteQuery - ultra-fast autocomplete suggestions
/// </summary>
public class GetAutocompleteQueryHandler : IRequestHandler<GetAutocompleteQuery, GetAutocompleteResult>
{
    private readonly IAutocompleteService _autocompleteService;
    private readonly ILogger<GetAutocompleteQueryHandler> _logger;

    public GetAutocompleteQueryHandler(
        IAutocompleteService autocompleteService,
        ILogger<GetAutocompleteQueryHandler> logger)
    {
        _autocompleteService = autocompleteService;
        _logger = logger;
    }

    public async Task<GetAutocompleteResult> Handle(GetAutocompleteQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting autocomplete suggestions for prefix: {Prefix}, MaxResults: {MaxResults}",
            request.Prefix, request.MaxResults);

        if (string.IsNullOrWhiteSpace(request.Prefix) || request.Prefix.Length < 2)
        {
            // Don't return suggestions for very short prefixes
            return new GetAutocompleteResult(Enumerable.Empty<AutocompleteSuggestion>());
        }

        var suggestions = await _autocompleteService.GetSuggestionsAsync(request.Prefix, request.MaxResults);

        var results = suggestions.Select(s => new AutocompleteSuggestion(s.Text, s.Type, s.Score)).ToList();

        _logger.LogDebug("Returning {Count} autocomplete suggestions for prefix: {Prefix}",
            results.Count, request.Prefix);

        return new GetAutocompleteResult(results);
    }
}

