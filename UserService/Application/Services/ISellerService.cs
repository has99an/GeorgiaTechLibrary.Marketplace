using UserService.Application.DTOs;

namespace UserService.Application.Services;

/// <summary>
/// Service interface for seller business logic
/// </summary>
public interface ISellerService
{
    /// <summary>
    /// Gets a seller profile by ID
    /// </summary>
    Task<SellerProfileDto?> GetSellerProfileAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a seller profile for a user
    /// </summary>
    Task<SellerProfileDto> CreateSellerProfileAsync(Guid userId, string? location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates seller location
    /// </summary>
    Task<SellerProfileDto> UpdateSellerLocationAsync(Guid sellerId, string? location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a book for sale
    /// </summary>
    Task<SellerBookListingDto> AddBookForSaleAsync(Guid sellerId, AddBookForSaleDto addDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all books listed by a seller
    /// </summary>
    Task<IEnumerable<SellerBookListingDto>> GetSellerBooksAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a book listing
    /// </summary>
    Task<SellerBookListingDto> UpdateBookListingAsync(Guid sellerId, Guid listingId, UpdateBookListingDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book from sale
    /// </summary>
    Task<bool> RemoveBookFromSaleAsync(Guid sellerId, Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates seller statistics from order completion
    /// </summary>
    Task UpdateSellerStatsFromOrderAsync(Guid sellerId, int booksSold, decimal? orderRating, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates listing quantity when books are sold (called from OrderPaid event)
    /// </summary>
    Task UpdateListingQuantityFromOrderAsync(Guid orderId, Guid orderItemId, string buyerId, Guid sellerId, string bookISBN, string? condition, int quantitySold, decimal unitPrice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sold books for a seller
    /// </summary>
    Task<IEnumerable<SellerBookListingDto>> GetSoldBooksAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates seller rating based on all reviews
    /// </summary>
    Task RecalculateSellerRatingAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sellers (admin only)
    /// </summary>
    Task<IEnumerable<SellerProfileDto>> GetAllSellersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a seller (admin only) - prevents them from selling
    /// </summary>
    Task<SellerProfileDto> DeactivateSellerAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a review for a seller from a completed order
    /// </summary>
    Task<SellerReviewDto> CreateReviewAsync(Guid customerId, CreateSellerReviewDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all reviews for a seller
    /// </summary>
    Task<IEnumerable<SellerReviewDto>> GetSellerReviewsAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing review
    /// </summary>
    Task<SellerReviewDto> UpdateReviewAsync(Guid reviewId, Guid customerId, UpdateSellerReviewDto updateDto, CancellationToken cancellationToken = default);
}


