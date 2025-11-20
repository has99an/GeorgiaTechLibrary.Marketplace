using MediatR;
using Microsoft.AspNetCore.Mvc;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Queries.Books;
using SearchService.Application.Queries.Search;
using SearchService.Application.Queries.Statistics;
using SearchService.Infrastructure.Common;
using StackExchange.Redis;

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
    private readonly IBookRepository _bookRepository;
    private readonly IConnectionMultiplexer _redis;

    public SearchController(
        IMediator mediator,
        ILogger<SearchController> logger,
        IBookRepository bookRepository,
        IConnectionMultiplexer redis)
    {
        _mediator = mediator;
        _logger = logger;
        _bookRepository = bookRepository;
        _redis = redis;
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

    /// <summary>
    /// Debug endpoint to inspect Redis state and diagnose issues
    /// </summary>
    /// <returns>Diagnostic information about Redis state</returns>
    /// <response code="200">Returns diagnostic information</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("debug")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetDebugInfo()
    {
        try
        {
            var database = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());

            // Count total books in Redis
            var totalBooks = 0;
            var booksWithStock = 0;
            var booksWithoutStock = 0;
            var sampleBooks = new List<object>();

            await foreach (var key in server.KeysAsync(pattern: "book:*", pageSize: 1000))
            {
                totalBooks++;
                var value = await database.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    try
                    {
                        var jsonString = value.ToString();
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
                        var root = doc.RootElement;
                        
                        var totalStock = root.TryGetProperty("totalStock", out var stock) ? stock.GetInt32() : 0;
                        var availableSellers = root.TryGetProperty("availableSellers", out var sellers) ? sellers.GetInt32() : 0;
                        
                        if (totalStock > 0 && availableSellers > 0)
                        {
                            booksWithStock++;
                        }
                        else
                        {
                            booksWithoutStock++;
                        }

                        // Collect sample books (first 5)
                        if (sampleBooks.Count < 5)
                        {
                            sampleBooks.Add(new
                            {
                                isbn = root.TryGetProperty("isbn", out var isbn) ? isbn.GetString() : "unknown",
                                title = root.TryGetProperty("title", out var title) ? title.GetString() : "unknown",
                                totalStock = totalStock,
                                availableSellers = availableSellers,
                                minPrice = root.TryGetProperty("minPrice", out var price) ? price.GetDecimal() : 0
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing book data for key {Key}", key);
                    }
                }
            }

            // Check sorted set sizes
            var titleSortedSetKey = RedisKeyBuilder.BuildAvailableBooksKey("title");
            var priceSortedSetKey = RedisKeyBuilder.BuildAvailableBooksKey("price");
            var titleSetSize = await database.SortedSetLengthAsync(titleSortedSetKey);
            var priceSetSize = await database.SortedSetLengthAsync(priceSortedSetKey);

            // Get sample ISBNs from sorted sets
            var titleSampleIsbns = await database.SortedSetRangeByRankAsync(titleSortedSetKey, 0, 4, Order.Ascending);
            var priceSampleIsbns = await database.SortedSetRangeByRankAsync(priceSortedSetKey, 0, 4, Order.Ascending);

            return Ok(new
            {
                redis = new
                {
                    connected = _redis.IsConnected,
                    endpoints = _redis.GetEndPoints().Select(e => e.ToString()).ToArray()
                },
                books = new
                {
                    total = totalBooks,
                    withStock = booksWithStock,
                    withoutStock = booksWithoutStock,
                    samples = sampleBooks
                },
                sortedSets = new
                {
                    title = new
                    {
                        key = titleSortedSetKey,
                        size = titleSetSize,
                        sampleIsbns = titleSampleIsbns.Select(v => v.ToString()).ToArray()
                    },
                    price = new
                    {
                        key = priceSortedSetKey,
                        size = priceSetSize,
                        sampleIsbns = priceSampleIsbns.Select(v => v.ToString()).ToArray()
                    }
                },
                diagnosis = new
                {
                    issue = booksWithStock == 0 ? "No books have stock information. Books need warehouse items to be available." : "OK",
                    recommendation = booksWithStock == 0 
                        ? "Sync warehouse items from WarehouseService to populate stock data." 
                        : titleSetSize == 0 && priceSetSize == 0 
                            ? "Sorted sets are empty. Rebuild sorted sets from books with stock." 
                            : "System appears healthy."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting debug information");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}

