using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using SearchService.DTOs;
using SearchService.Repositories;

namespace SearchService.Controllers;

/// <summary>
/// Search controller for book search and discovery functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchRepository _searchRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ISearchRepository searchRepository,
        IMapper mapper,
        ILogger<SearchController> logger)
    {
        _searchRepository = searchRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Search for books by title, author, or ISBN
    /// </summary>
    /// <param name="query">Search term to find books</param>
    /// <returns>List of books matching the search query</returns>
    /// <response code="200">Returns the list of matching books</response>
    /// <response code="400">If the query parameter is missing or empty</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> SearchBooks([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            var books = await _searchRepository.SearchBooksAsync(query);
            var resultDtos = _mapper.Map<IEnumerable<SearchResultDto>>(books);

            return Ok(resultDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching books with query {Query}", query);
            return StatusCode(500, "Internal server error");
        }
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
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> GetAvailableBooks(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = "asc")
    {
        try
        {
            var result = await _searchRepository.GetAvailableBooksAsync(page, pageSize, sortBy, sortOrder);
            var resultDtos = _mapper.Map<IEnumerable<SearchResultDto>>(result.Items);

            return Ok(new 
            {
                books = resultDtos,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount,
                totalPages = result.TotalPages,
                hasNextPage = result.HasNextPage,
                hasPreviousPage = result.HasPreviousPage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available books");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get featured/recommended books
    /// </summary>
    /// <returns>List of featured books</returns>
    /// <response code="200">Returns the list of featured books</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(IEnumerable<SearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> GetFeaturedBooks()
    {
        try
        {
            var books = await _searchRepository.GetFeaturedBooksAsync();
            var resultDtos = _mapper.Map<IEnumerable<SearchResultDto>>(books);

            return Ok(resultDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving featured books");
            return StatusCode(500, "Internal server error");
        }
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
    [ProducesResponseType(typeof(SearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResultDto>> GetBookByIsbn(string isbn)
    {
        try
        {
            var book = await _searchRepository.GetBookByIsbnAsync(isbn);
            if (book == null)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            var resultDto = _mapper.Map<SearchResultDto>(book);
            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving book with ISBN {Isbn}", isbn);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all sellers offering a specific book with prices and stock
    /// </summary>
    /// <param name="isbn">The ISBN of the book</param>
    /// <returns>List of sellers with pricing and stock information</returns>
    /// <response code="200">Returns the list of sellers</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("sellers/{isbn}")]
    [ProducesResponseType(typeof(IEnumerable<SellerInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SellerInfoDto>>> GetBookSellers(string isbn)
    {
        try
        {
            var sellers = await _searchRepository.GetBookSellersAsync(isbn);
            return Ok(sellers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sellers for book with ISBN {Isbn}", isbn);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Health check endpoint for service monitoring
    /// </summary>
    /// <returns>Service health status and features</returns>
    /// <response code="200">Service is healthy</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            service = "SearchService",
            features = new {
                search = true,
                availability_filtering = true,
                seller_information = true
            }
        });
    }

    /// <summary>
    /// Get search service statistics
    /// </summary>
    /// <returns>Statistics about search operations and performance</returns>
    /// <response code="200">Returns search statistics</response>
    /// <response code="500">If an internal server error occurs</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SearchStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchStatsDto>> GetStats()
    {
        try
        {
            var stats = await _searchRepository.GetSearchStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search statistics");
            return StatusCode(500, "Internal server error");
        }
    }
}