using MediatR;
using Microsoft.AspNetCore.Mvc;
using SearchService.Application.Queries.Books;
using SearchService.Application.Queries.Search;
using SearchService.Application.Queries.Statistics;

namespace SearchService.API.Controllers;

/// <summary>
/// Search controller for book search and discovery functionality - Clean Architecture version
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IMediator mediator,
        ILogger<SearchController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Search for books by title, author, or ISBN with pagination and sorting
    /// </summary>
    /// <param name="query">Search term to find books</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100)</param>
    /// <param name="sortBy">Sort by: relevance (default), title, price, rating</param>
    /// <returns>Paginated list of books matching the search query</returns>
    /// <response code="200">Returns the paginated list of matching books</response>
    /// <response code="400">If the query parameter is missing or empty</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet]
    [ProducesResponseType(typeof(SearchBooksResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SearchBooks(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "relevance")
    {
        var result = await _mediator.Send(new SearchBooksQuery(query, page, pageSize, sortBy));
        return Ok(result);
    }

    /// <summary>
    /// Get available books with pagination and sorting
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 20)</param>
    /// <param name="sortBy">Field to sort by (e.g., "price", "title")</param>
    /// <param name="sortOrder">Sort order: "asc" or "desc" (default: "asc")</param>
    /// <returns>Paginated list of available books</returns>
    /// <response code="200">Returns the list of available books</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("available")]
    [ProducesResponseType(typeof(GetAvailableBooksResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetAvailableBooks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = "asc")
    {
        var query = new GetAvailableBooksQuery(page, pageSize, sortBy, sortOrder);
        var result = await _mediator.Send(query);
        
        return Ok(new
        {
            books = result.Books.Items,
            page = result.Books.Page,
            pageSize = result.Books.PageSize,
            totalCount = result.Books.TotalCount,
            totalPages = result.Books.TotalPages,
            hasNextPage = result.Books.HasNextPage,
            hasPreviousPage = result.Books.HasPreviousPage
        });
    }

    /// <summary>
    /// Get featured/recommended books
    /// </summary>
    /// <returns>List of featured books</returns>
    /// <response code="200">Returns the list of featured books</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(GetFeaturedBooksResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetFeaturedBooks()
    {
        var result = await _mediator.Send(new GetFeaturedBooksQuery());
        return Ok(result);
    }

    /// <summary>
    /// Get book details by ISBN with stock and pricing information
    /// </summary>
    /// <param name="isbn">The ISBN of the book to retrieve</param>
    /// <returns>Book details with stock and pricing</returns>
    /// <response code="200">Returns the book details</response>
    /// <response code="404">If the book is not found</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("by-isbn/{isbn}")]
    [ProducesResponseType(typeof(GetBookByIsbnResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetBookByIsbn(string isbn)
    {
        var result = await _mediator.Send(new GetBookByIsbnQuery(isbn));
        
        if (result.Book == null)
        {
            return NotFound($"Book with ISBN {isbn} not found");
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all sellers offering a specific book with prices and stock
    /// </summary>
    /// <param name="isbn">The ISBN of the book</param>
    /// <returns>List of sellers with pricing and stock information</returns>
    /// <response code="200">Returns the list of sellers</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("sellers/{isbn}")]
    [ProducesResponseType(typeof(GetBookSellersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetBookSellers(string isbn)
    {
        var result = await _mediator.Send(new GetBookSellersQuery(isbn));
        return Ok(result);
    }

    /// <summary>
    /// Basic health check endpoint for service monitoring
    /// </summary>
    /// <returns>Service health status</returns>
    /// <response code="200">Service is healthy</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "SearchService"
        });
    }

    /// <summary>
    /// Get search service statistics
    /// </summary>
    /// <returns>Statistics about search operations and performance</returns>
    /// <response code="200">Returns search statistics</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(GetSearchStatsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetStats()
    {
        var result = await _mediator.Send(new GetSearchStatsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Get autocomplete suggestions for search (ultra-fast typeahead)
    /// </summary>
    /// <param name="prefix">The prefix to search for (minimum 2 characters)</param>
    /// <param name="maxResults">Maximum number of suggestions to return (default: 10, max: 50)</param>
    /// <returns>List of autocomplete suggestions with type and score</returns>
    /// <response code="200">Returns autocomplete suggestions</response>
    /// <response code="400">If prefix is invalid</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(GetAutocompleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetAutocomplete(
        [FromQuery] string prefix,
        [FromQuery] int maxResults = 10)
    {
        var result = await _mediator.Send(new GetAutocompleteQuery(prefix, maxResults));
        return Ok(result);
    }

    /// <summary>
    /// Get available facets for filtering search results
    /// </summary>
    /// <param name="searchTerm">Optional search term to get facets for specific search results</param>
    /// <returns>Available facets with counts</returns>
    /// <response code="200">Returns available facets</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("facets")]
    [ProducesResponseType(typeof(GetSearchFacetsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetFacets([FromQuery] string? searchTerm = null)
    {
        var result = await _mediator.Send(new GetSearchFacetsQuery(searchTerm));
        return Ok(result);
    }

    /// <summary>
    /// Advanced search with multiple filters (faceted search)
    /// </summary>
    /// <param name="request">Filter criteria</param>
    /// <returns>Filtered and paginated book results</returns>
    /// <response code="200">Returns filtered books</response>
    /// <response code="400">If filter parameters are invalid</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpPost("advanced")]
    [ProducesResponseType(typeof(SearchBooksWithFiltersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SearchWithFilters([FromBody] SearchBooksWithFiltersQuery request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    /// <summary>
    /// Get popular/trending searches
    /// </summary>
    /// <param name="topN">Number of top searches to return (default: 10)</param>
    /// <param name="timeWindow">Time window: 24h (default) or all</param>
    /// <returns>List of popular searches with counts</returns>
    /// <response code="200">Returns popular searches</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("popular")]
    [ProducesResponseType(typeof(Application.Queries.Analytics.GetPopularSearchesResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetPopularSearches(
        [FromQuery] int topN = 10,
        [FromQuery] string timeWindow = "24h")
    {
        var result = await _mediator.Send(new Application.Queries.Analytics.GetPopularSearchesQuery(topN, timeWindow));
        return Ok(result);
    }
}

