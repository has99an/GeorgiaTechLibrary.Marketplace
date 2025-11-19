namespace SearchService.Domain.Services;

/// <summary>
/// Service for autocomplete functionality
/// </summary>
public interface IAutocompleteService
{
    /// <summary>
    /// Gets autocomplete suggestions for a given prefix
    /// </summary>
    Task<IEnumerable<(string Text, string Type, double Score)>> GetSuggestionsAsync(string prefix, int maxResults = 10);

    /// <summary>
    /// Indexes a term for autocomplete
    /// </summary>
    Task IndexTermAsync(string term, string type, double score = 1.0);

    /// <summary>
    /// Records a click/selection for popularity tracking
    /// </summary>
    Task RecordSelectionAsync(string term);
}

