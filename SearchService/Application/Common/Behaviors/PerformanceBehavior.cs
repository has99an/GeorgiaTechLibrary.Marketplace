using MediatR;
using SearchService.Application.Commands.Analytics;
using SearchService.Application.Queries.Books;
using System.Diagnostics;

namespace SearchService.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior for monitoring performance, logging slow requests, and auto-tracking searches
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMediator _mediator;
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int SlowRequestThresholdMs = 500;

    public PerformanceBehavior(
        IMediator mediator,
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var response = await next();
        
        stopwatch.Stop();

        var requestName = typeof(TRequest).Name;

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            _logger.LogWarning("Slow request detected: {RequestName} took {ElapsedMilliseconds}ms",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        // Auto-track search queries
        if (request is SearchBooksQuery searchQuery && response is SearchBooksResult searchResult)
        {
            try
            {
                // Fire and forget - don't wait for tracking
                _ = _mediator.Send(new TrackSearchCommand(
                    searchQuery.SearchTerm,
                    searchResult.Books.TotalCount,
                    stopwatch.ElapsedMilliseconds
                ), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-tracking search");
            }
        }

        return response;
    }
}

