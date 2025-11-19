using MediatR;
using SearchService.Application.Common.Interfaces;

namespace SearchService.Application.Commands.Analytics;

/// <summary>
/// Handler for TrackSearchCommand
/// </summary>
public class TrackSearchCommandHandler : IRequestHandler<TrackSearchCommand, TrackSearchResult>
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<TrackSearchCommandHandler> _logger;

    public TrackSearchCommandHandler(
        IAnalyticsRepository analyticsRepository,
        ILogger<TrackSearchCommandHandler> _logger)
    {
        _analyticsRepository = analyticsRepository;
        this._logger = _logger;
    }

    public async Task<TrackSearchResult> Handle(TrackSearchCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _analyticsRepository.TrackSearchAsync(
                request.SearchTerm,
                request.ResultCount,
                request.ResponseTimeMs
            );

            if (!string.IsNullOrWhiteSpace(request.ClickedIsbn))
            {
                await _analyticsRepository.TrackClickThroughAsync(request.SearchTerm, request.ClickedIsbn);
            }

            _logger.LogDebug("Tracked search: {SearchTerm}, Results: {ResultCount}, Time: {ResponseTime}ms",
                request.SearchTerm, request.ResultCount, request.ResponseTimeMs);

            return new TrackSearchResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking search: {SearchTerm}", request.SearchTerm);
            return new TrackSearchResult(false);
        }
    }
}

