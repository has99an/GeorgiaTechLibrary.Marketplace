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
}

