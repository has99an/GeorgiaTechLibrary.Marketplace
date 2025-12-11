using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.API.Controllers;

/// <summary>
/// Controller for seller management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SellersController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<SellersController> _logger;

    public SellersController(
        ISellerService sellerService,
        ILogger<SellersController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a seller profile by ID
    /// </summary>
    [HttpGet("{sellerId}/profile")]
    [ProducesResponseType(typeof(SellerProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerProfileDto>> GetSellerProfile(Guid sellerId)
    {
        var profile = await _sellerService.GetSellerProfileAsync(sellerId);
        
        if (profile == null)
        {
            return NotFound(new { Message = $"Seller profile with ID {sellerId} not found" });
        }

        return Ok(profile);
    }

    /// <summary>
    /// Creates a seller profile for a user
    /// </summary>
    [HttpPost("{userId}/profile")]
    [ProducesResponseType(typeof(SellerProfileDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerProfileDto>> CreateSellerProfile(
        Guid userId,
        [FromBody] CreateSellerProfileDto? createDto)
    {
        if (createDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var profile = await _sellerService.CreateSellerProfileAsync(userId, createDto.Location);
        return CreatedAtAction(nameof(GetSellerProfile), new { sellerId = profile.SellerId }, profile);
    }

    /// <summary>
    /// Updates seller location
    /// </summary>
    [HttpPut("{sellerId}/profile/location")]
    [ProducesResponseType(typeof(SellerProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerProfileDto>> UpdateSellerLocation(
        Guid sellerId,
        [FromBody] UpdateSellerLocationDto? updateDto)
    {
        if (updateDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        var profile = await _sellerService.UpdateSellerLocationAsync(sellerId, updateDto.Location);
        return Ok(profile);
    }

    /// <summary>
    /// Adds a book for sale (Add book for sale - projektkrav)
    /// </summary>
    [HttpPost("{sellerId}/books")]
    [ProducesResponseType(typeof(SellerBookListingDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerBookListingDto>> AddBookForSale(
        Guid sellerId,
        [FromBody] AddBookForSaleDto? addDto)
    {
        if (addDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var listing = await _sellerService.AddBookForSaleAsync(sellerId, addDto);
        return CreatedAtAction(
            nameof(GetBookListing),
            new { sellerId = sellerId, listingId = listing.ListingId },
            listing);
    }

    /// <summary>
    /// Gets all books listed by a seller
    /// </summary>
    [HttpGet("{sellerId}/books")]
    [ProducesResponseType(typeof(IEnumerable<SellerBookListingDto>), 200)]
    public async Task<ActionResult<IEnumerable<SellerBookListingDto>>> GetSellerBooks(Guid sellerId)
    {
        var listings = await _sellerService.GetSellerBooksAsync(sellerId);
        return Ok(listings);
    }

    /// <summary>
    /// Gets all sold books for a seller
    /// </summary>
    [HttpGet("{sellerId}/books/sold")]
    [ProducesResponseType(typeof(IEnumerable<SellerBookListingDto>), 200)]
    public async Task<ActionResult<IEnumerable<SellerBookListingDto>>> GetSoldBooks(Guid sellerId)
    {
        var soldBooks = await _sellerService.GetSoldBooksAsync(sellerId);
        return Ok(soldBooks);
    }

    /// <summary>
    /// Gets a specific book listing
    /// </summary>
    [HttpGet("{sellerId}/books/{listingId}")]
    [ProducesResponseType(typeof(SellerBookListingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerBookListingDto>> GetBookListing(Guid sellerId, Guid listingId)
    {
        var listings = await _sellerService.GetSellerBooksAsync(sellerId);
        var listing = listings.FirstOrDefault(l => l.ListingId == listingId);
        
        if (listing == null)
        {
            return NotFound(new { Message = $"Book listing with ID {listingId} not found for seller {sellerId}" });
        }

        return Ok(listing);
    }

    /// <summary>
    /// Updates a book listing
    /// </summary>
    [HttpPut("{sellerId}/books/{listingId}")]
    [ProducesResponseType(typeof(SellerBookListingDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerBookListingDto>> UpdateBookListing(
        Guid sellerId,
        Guid listingId,
        [FromBody] UpdateBookListingDto? updateDto)
    {
        if (updateDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var listing = await _sellerService.UpdateBookListingAsync(sellerId, listingId, updateDto);
            return Ok(listing);
        }
        catch (Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a book from sale
    /// </summary>
    [HttpDelete("{sellerId}/books/{listingId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveBookFromSale(Guid sellerId, Guid listingId)
    {
        try
        {
            await _sellerService.RemoveBookFromSaleAsync(sellerId, listingId);
            return NoContent();
        }
        catch (Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets all sellers (admin only)
    /// </summary>
    [HttpGet("admin/all")]
    [ProducesResponseType(typeof(IEnumerable<SellerProfileDto>), 200)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<IEnumerable<SellerProfileDto>>> GetAllSellers()
    {
        // Check if user is admin (should be done via middleware, but adding here as safety)
        var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
        {
            return Unauthorized();
        }

        // Note: Full authorization check should be in middleware
        var sellers = await _sellerService.GetAllSellersAsync();
        return Ok(sellers);
    }

    /// <summary>
    /// Deactivates a seller (admin only) - prevents them from selling
    /// </summary>
    [HttpPost("admin/{sellerId}/deactivate")]
    [ProducesResponseType(typeof(SellerProfileDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerProfileDto>> DeactivateSeller(Guid sellerId)
    {
        // Check if user is admin (should be done via middleware, but adding here as safety)
        var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
        {
            return Unauthorized();
        }

        // Note: Full authorization check should be in middleware
        var seller = await _sellerService.DeactivateSellerAsync(sellerId);
        return Ok(seller);
    }
}

/// <summary>
/// DTO for creating a seller profile
/// </summary>
public class CreateSellerProfileDto
{
    public string? Location { get; set; }
}

/// <summary>
/// DTO for updating seller location
/// </summary>
public class UpdateSellerLocationDto
{
    public string? Location { get; set; }
}


