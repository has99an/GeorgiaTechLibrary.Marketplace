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

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
