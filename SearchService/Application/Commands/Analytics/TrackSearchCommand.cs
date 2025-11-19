using MediatR;

namespace SearchService.Application.Commands.Analytics;

/// <summary>
/// Command to track a search query
/// </summary>
public record TrackSearchCommand(
    string SearchTerm,
    int ResultCount,
    long ResponseTimeMs,
    string? ClickedIsbn = null
) : IRequest<TrackSearchResult>;

/// <summary>
/// Result of track search command
/// </summary>
public record TrackSearchResult(bool Success);

