using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using SearchService.DTOs;
using SearchService.Repositories;

namespace SearchService.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    [HttpGet]
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

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> GetAvailableBooks(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = "asc")
    {
        try
        {
            var books = await _searchRepository.GetAvailableBooksAsync(page, pageSize, sortBy, sortOrder);
            var resultDtos = _mapper.Map<IEnumerable<SearchResultDto>>(books);

            return Ok(new 
            {
                books = resultDtos,
                page,
                pageSize,
                totalCount = books.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available books");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("featured")]
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

    [HttpGet("by-isbn/{isbn}")]
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

    [HttpGet("sellers/{isbn}")]
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

    [HttpGet("health")]
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

    [HttpGet("stats")]
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