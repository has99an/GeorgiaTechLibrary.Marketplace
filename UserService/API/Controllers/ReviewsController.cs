using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.API.Controllers;

/// <summary>
/// Controller for seller review operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        ISellerService sellerService,
        ILogger<ReviewsController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a review for a seller from a completed order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SellerReviewDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<SellerReviewDto>> CreateReview([FromBody] CreateSellerReviewDto? createDto)
    {
        if (createDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get customer ID from header (set by ApiGateway)
        var customerIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(customerIdHeader) || !Guid.TryParse(customerIdHeader, out var customerId))
        {
            return Unauthorized(new { Message = "User ID is required" });
        }

        try
        {
            var review = await _sellerService.CreateReviewAsync(customerId, createDto);
            return CreatedAtAction(nameof(GetReview), new { reviewId = review.ReviewId }, review);
        }
        catch (UserService.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets all reviews for a seller
    /// </summary>
    [HttpGet("seller/{sellerId}")]
    [ProducesResponseType(typeof(IEnumerable<SellerReviewDto>), 200)]
    public async Task<ActionResult<IEnumerable<SellerReviewDto>>> GetSellerReviews(Guid sellerId)
    {
        var reviews = await _sellerService.GetSellerReviewsAsync(sellerId);
        return Ok(reviews);
    }

    /// <summary>
    /// Gets a specific review by ID
    /// </summary>
    [HttpGet("{reviewId}")]
    [ProducesResponseType(typeof(SellerReviewDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerReviewDto>> GetReview(Guid reviewId)
    {
        // For now, get all reviews and find the one - could be optimized with a GetById method
        var allSellers = await _sellerService.GetAllSellersAsync();
        foreach (var seller in allSellers)
        {
            var reviews = await _sellerService.GetSellerReviewsAsync(seller.SellerId);
            var review = reviews.FirstOrDefault(r => r.ReviewId == reviewId);
            if (review != null)
            {
                return Ok(review);
            }
        }

        return NotFound(new { Message = $"Review with ID {reviewId} not found" });
    }

    /// <summary>
    /// Updates an existing review
    /// </summary>
    [HttpPut("{reviewId}")]
    [ProducesResponseType(typeof(SellerReviewDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SellerReviewDto>> UpdateReview(
        Guid reviewId,
        [FromBody] UpdateSellerReviewDto? updateDto)
    {
        if (updateDto == null)
        {
            return BadRequest(new { Message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get customer ID from header (set by ApiGateway)
        var customerIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(customerIdHeader) || !Guid.TryParse(customerIdHeader, out var customerId))
        {
            return Unauthorized(new { Message = "User ID is required" });
        }

        try
        {
            var review = await _sellerService.UpdateReviewAsync(reviewId, customerId, updateDto);
            return Ok(review);
        }
        catch (UserService.Domain.Exceptions.ValidationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (UserService.Domain.Exceptions.UnauthorizedException ex)
        {
            return Unauthorized(new { Message = ex.Message });
        }
    }
}




