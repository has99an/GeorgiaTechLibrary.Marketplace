using MediatR;
using SearchService.Application.Common.Models;

namespace SearchService.Application.Queries.Statistics;

/// <summary>
/// Query to get search service statistics
/// </summary>
public record GetSearchStatsQuery : IRequest<GetSearchStatsResult>;

/// <summary>
/// Result of get search stats query
/// </summary>
public record GetSearchStatsResult(SearchStatsDto Stats);

