using AutoMapper;
using Microsoft.Extensions.Logging;
using UserService.Application.DTOs;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using UserService.Domain.Exceptions;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Services;

/// <summary>
/// Service implementation for seller business logic
/// </summary>
public class SellerService : ISellerService
{
    private readonly ISellerRepository _sellerRepository;
    private readonly ISellerBookListingRepository _listingRepository;
    private readonly IBookSaleRepository _bookSaleRepository;
    private readonly ISellerReviewRepository _reviewRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IMapper _mapper;
    private readonly ILogger<SellerService> _logger;

    public SellerService(
        ISellerRepository sellerRepository,
        ISellerBookListingRepository listingRepository,
        IBookSaleRepository bookSaleRepository,
        ISellerReviewRepository reviewRepository,
        IUserRepository userRepository,
        IMessageProducer messageProducer,
        IMapper mapper,
        ILogger<SellerService> logger)
    {
        _sellerRepository = sellerRepository;
        _listingRepository = listingRepository;
        _bookSaleRepository = bookSaleRepository;
        _reviewRepository = reviewRepository;
        _userRepository = userRepository;
        _messageProducer = messageProducer;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SellerProfileDto?> GetSellerProfileAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            return null;
        }

        var dto = _mapper.Map<SellerProfileDto>(sellerProfile);
        // Map user information
        if (sellerProfile.User != null)
        {
            dto.Name = sellerProfile.User.Name;
            dto.Email = sellerProfile.User.GetEmailString();
        }

        return dto;
    }

    public async Task<SellerProfileDto> CreateSellerProfileAsync(Guid userId, string? location, CancellationToken cancellationToken = default)
    {
        // Check if user exists
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // Check if seller profile already exists
        var existingProfile = await _sellerRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existingProfile != null)
        {
            var dto = _mapper.Map<SellerProfileDto>(existingProfile);
            dto.Name = user.Name;
            dto.Email = user.GetEmailString();
            return dto;
        }

        // Create seller profile
        var sellerProfile = SellerProfile.Create(userId, location);
        var createdProfile = await _sellerRepository.AddAsync(sellerProfile, cancellationToken);

        _logger.LogInformation("Seller profile created: {SellerId}, Location: {Location}", 
            createdProfile.SellerId, location);

        // Publish SellerCreated event
        PublishSellerCreatedEvent(createdProfile, user);

        var result = _mapper.Map<SellerProfileDto>(createdProfile);
        result.Name = user.Name;
        result.Email = user.GetEmailString();
        return result;
    }

    public async Task<SellerProfileDto> UpdateSellerLocationAsync(Guid sellerId, string? location, CancellationToken cancellationToken = default)
    {
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            throw new SellerNotFoundException(sellerId);
        }

        sellerProfile.UpdateLocation(location);
        var updatedProfile = await _sellerRepository.UpdateAsync(sellerProfile, cancellationToken);

        _logger.LogInformation("Seller location updated: {SellerId}, Location: {Location}", 
            sellerId, location);

        // Publish SellerUpdated event
        PublishSellerUpdatedEvent(updatedProfile);

        var dto = _mapper.Map<SellerProfileDto>(updatedProfile);
        if (updatedProfile.User != null)
        {
            dto.Name = updatedProfile.User.Name;
            dto.Email = updatedProfile.User.GetEmailString();
        }
        return dto;
    }

    public async Task<SellerBookListingDto> AddBookForSaleAsync(Guid sellerId, AddBookForSaleDto addDto, CancellationToken cancellationToken = default)
    {
        // Verify seller exists
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            throw new SellerNotFoundException(sellerId);
        }

        // Check if listing already exists for this seller, book, and condition
        var existingListing = await _listingRepository.GetBySellerAndBookAsync(
            sellerId, addDto.BookISBN, addDto.Condition, cancellationToken);

        if (existingListing != null)
        {
            // Update existing listing instead of creating duplicate
            existingListing.UpdatePrice(addDto.Price);
            existingListing.UpdateQuantity(addDto.Quantity);
            existingListing.Activate();

            var updatedListing = await _listingRepository.UpdateAsync(existingListing, cancellationToken);
            _logger.LogInformation("Book listing updated: {ListingId}, Seller: {SellerId}, ISBN: {ISBN}", 
                updatedListing.ListingId, sellerId, addDto.BookISBN);

            // Publish BookAddedForSale event
            PublishBookAddedForSaleEvent(updatedListing);

            return _mapper.Map<SellerBookListingDto>(updatedListing);
        }

        // Create new listing
        var listing = SellerBookListing.Create(
            sellerId,
            addDto.BookISBN,
            addDto.Price,
            addDto.Quantity,
            addDto.Condition);

        var createdListing = await _listingRepository.AddAsync(listing, cancellationToken);

        _logger.LogInformation("Book added for sale: {ListingId}, Seller: {SellerId}, ISBN: {ISBN}, Price: {Price}", 
            createdListing.ListingId, sellerId, addDto.BookISBN, addDto.Price);

        // Publish BookAddedForSale event
        PublishBookAddedForSaleEvent(createdListing);

        return _mapper.Map<SellerBookListingDto>(createdListing);
    }

    public async Task<IEnumerable<SellerBookListingDto>> GetSellerBooksAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var listings = await _listingRepository.GetBySellerIdAsync(sellerId, cancellationToken);
        var dtos = _mapper.Map<IEnumerable<SellerBookListingDto>>(listings).ToList();
        
        // Populate sales for each listing
        foreach (var dto in dtos)
        {
            var sales = await _bookSaleRepository.GetByListingIdAsync(dto.ListingId, cancellationToken);
            dto.Sales = _mapper.Map<List<BookSaleDto>>(sales);
        }
        
        return dtos;
    }

    public async Task<SellerBookListingDto> UpdateBookListingAsync(Guid sellerId, Guid listingId, UpdateBookListingDto updateDto, CancellationToken cancellationToken = default)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing == null)
        {
            throw new BookListingNotFoundException(listingId);
        }

        if (listing.SellerId != sellerId)
        {
            throw new ValidationException("SellerId", $"Listing {listingId} does not belong to seller {sellerId}");
        }

        if (listing.IsSold)
        {
            throw new ValidationException("Listing", $"Cannot update a sold listing. Listing {listingId} was sold on {listing.SoldDate}");
        }

        if (updateDto.Price.HasValue)
        {
            listing.UpdatePrice(updateDto.Price.Value);
        }

        if (updateDto.Quantity.HasValue)
        {
            listing.UpdateQuantity(updateDto.Quantity.Value);
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Condition))
        {
            listing.UpdateCondition(updateDto.Condition);
        }

        if (updateDto.IsActive.HasValue)
        {
            if (updateDto.IsActive.Value)
            {
                listing.Activate();
            }
            else
            {
                listing.Deactivate();
            }
        }

        var updatedListing = await _listingRepository.UpdateAsync(listing, cancellationToken);

        _logger.LogInformation("Book listing updated: {ListingId}, Seller: {SellerId}", 
            listingId, sellerId);

        return _mapper.Map<SellerBookListingDto>(updatedListing);
    }

    public async Task<bool> RemoveBookFromSaleAsync(Guid sellerId, Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing == null)
        {
            throw new BookListingNotFoundException(listingId);
        }

        if (listing.SellerId != sellerId)
        {
            throw new ValidationException("SellerId", $"Listing {listingId} does not belong to seller {sellerId}");
        }

        if (listing.IsSold)
        {
            throw new ValidationException("Listing", $"Cannot remove a sold listing from sale. Listing {listingId} was sold on {listing.SoldDate}");
        }

        listing.Deactivate();
        await _listingRepository.UpdateAsync(listing, cancellationToken);

        _logger.LogInformation("Book removed from sale: {ListingId}, Seller: {SellerId}", 
            listingId, sellerId);

        return true;
    }

    public async Task UpdateSellerStatsFromOrderAsync(Guid sellerId, int booksSold, decimal? orderRating, CancellationToken cancellationToken = default)
    {
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            _logger.LogWarning("Seller profile not found for stats update: {SellerId}", sellerId);
            return;
        }

        sellerProfile.UpdateFromOrder(booksSold, orderRating);
        await _sellerRepository.UpdateAsync(sellerProfile, cancellationToken);

        _logger.LogInformation("Seller stats updated: {SellerId}, BooksSold: {BooksSold}, Rating: {Rating}", 
            sellerId, booksSold, orderRating);

        // Publish SellerUpdated event
        PublishSellerUpdatedEvent(sellerProfile);
    }

    public async Task UpdateListingQuantityFromOrderAsync(Guid orderId, Guid orderItemId, string buyerId, Guid sellerId, string bookISBN, string? condition, int quantitySold, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating listing quantity from order - OrderId: {OrderId}, OrderItemId: {OrderItemId}, BuyerId: {BuyerId}, SellerId: {SellerId}, BookISBN: {BookISBN}, Condition: {Condition}, QuantitySold: {QuantitySold}, UnitPrice: {UnitPrice}",
            orderId, orderItemId, buyerId, sellerId, bookISBN, condition ?? "N/A", quantitySold, unitPrice);

        SellerBookListing? listing = null;

        // First, try to find by condition if provided
        if (!string.IsNullOrWhiteSpace(condition))
        {
            listing = await _listingRepository.GetBySellerAndBookAsync(sellerId, bookISBN, condition, cancellationToken);
        }

        // If not found by condition, get all listings for this seller and book
        if (listing == null)
        {
            var allListings = await _listingRepository.GetBySellerIdAsync(sellerId, cancellationToken);
            var activeListings = allListings
                .Where(l => l.BookISBN == bookISBN && l.IsActive && !l.IsSold)
                .ToList();

            if (activeListings.Count == 0)
            {
                _logger.LogWarning("No active listing found for quantity update - SellerId: {SellerId}, BookISBN: {BookISBN}",
                    sellerId, bookISBN);
                return;
            }

            if (activeListings.Count == 1)
            {
                listing = activeListings.First();
                _logger.LogInformation("Found single active listing (condition not matched) - ListingId: {ListingId}, Condition: {Condition}",
                    listing.ListingId, listing.Condition);
            }
            else
            {
                // Multiple active listings - use the first one and log warning
                listing = activeListings.First();
                _logger.LogWarning("Multiple active listings found for SellerId: {SellerId}, BookISBN: {BookISBN}. Using first listing (ListingId: {ListingId}, Condition: {Condition}). Total active listings: {Count}",
                    sellerId, bookISBN, listing.ListingId, listing.Condition, activeListings.Count);
            }
        }

        if (listing == null)
        {
            _logger.LogWarning("Listing not found for quantity update - SellerId: {SellerId}, BookISBN: {BookISBN}",
                sellerId, bookISBN);
            return;
        }

        try
        {
            var oldQuantity = listing.Quantity;
            var wasActive = listing.IsActive;

            // Decrease quantity - this method handles validation and sets IsActive to false if quantity reaches 0
            listing.DecreaseQuantity(quantitySold);
            
            // Mark as sold if quantity reaches 0
            var wasSold = false;
            if (listing.Quantity == 0)
            {
                listing.MarkAsSold();
                wasSold = true;
                _logger.LogInformation("Listing marked as sold - ListingId: {ListingId}", listing.ListingId);
            }
            
            // Save the updated listing
            await _listingRepository.UpdateAsync(listing, cancellationToken);

            // Create BookSale record
            var bookSale = BookSale.Create(
                listing.ListingId,
                orderId,
                orderItemId,
                buyerId,
                bookISBN,
                sellerId,
                quantitySold,
                unitPrice,
                listing.Condition);

            await _bookSaleRepository.AddAsync(bookSale, cancellationToken);

            _logger.LogInformation("Listing quantity updated and BookSale created - ListingId: {ListingId}, Condition: {Condition}, OldQuantity: {OldQuantity}, NewQuantity: {NewQuantity}, WasActive: {WasActive}, IsActive: {IsActive}, IsSold: {IsSold}, SaleId: {SaleId}",
                listing.ListingId, listing.Condition, oldQuantity, listing.Quantity, wasActive, listing.IsActive, listing.IsSold, bookSale.SaleId);

            // Always publish BookStockUpdated event when quantity changes to update SearchService
            // This ensures SearchService has the latest stock information from UserService
            await PublishBookStockUpdatedEventAsync(bookISBN, cancellationToken);

            // Publish BookSold event if listing was marked as sold
            if (wasSold)
            {
                PublishBookSoldEvent(listing, buyerId, orderId, orderItemId, quantitySold);
            }
        }
        catch (Domain.Exceptions.ValidationException ex)
        {
            // Handle validation errors (e.g., quantity would go negative)
            _logger.LogWarning(ex, "Failed to decrease listing quantity - ListingId: {ListingId}, RequestedQuantity: {QuantitySold}, CurrentQuantity: {CurrentQuantity}",
                listing.ListingId, quantitySold, listing.Quantity);
            // Don't throw - log and continue processing other items
        }
    }

    public async Task<IEnumerable<SellerBookListingDto>> GetSoldBooksAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var listings = await _listingRepository.GetBySellerIdAsync(sellerId, cancellationToken);
        var soldListings = listings.Where(l => l.IsSold).ToList();

        var result = new List<SellerBookListingDto>();

        foreach (var listing in soldListings)
        {
            var dto = _mapper.Map<SellerBookListingDto>(listing);
            
            // Get sales for this listing
            var sales = await _bookSaleRepository.GetByListingIdAsync(listing.ListingId, cancellationToken);
            dto.Sales = _mapper.Map<List<BookSaleDto>>(sales);
            
            result.Add(dto);
        }

        return result.OrderByDescending(l => l.SoldDate);
    }

    /// <summary>
    /// Recalculates seller rating based on all reviews
    /// </summary>
    public async Task RecalculateSellerRatingAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            _logger.LogWarning("Seller profile not found for rating recalculation: {SellerId}", sellerId);
            return;
        }

        var averageRating = await _reviewRepository.GetAverageRatingAsync(sellerId, cancellationToken);
        sellerProfile.UpdateRating(averageRating);
        await _sellerRepository.UpdateAsync(sellerProfile, cancellationToken);

        _logger.LogInformation("Seller rating recalculated: {SellerId}, NewRating: {Rating}", 
            sellerId, averageRating);

        // Publish SellerUpdated event
        PublishSellerUpdatedEvent(sellerProfile);
    }

    /// <summary>
    /// Gets all sellers (admin only)
    /// </summary>
    public async Task<IEnumerable<SellerProfileDto>> GetAllSellersAsync(CancellationToken cancellationToken = default)
    {
        var sellerProfiles = await _sellerRepository.GetAllAsync(cancellationToken);
        var dtos = new List<SellerProfileDto>();

        foreach (var profile in sellerProfiles)
        {
            var dto = _mapper.Map<SellerProfileDto>(profile);
            if (profile.User != null)
            {
                dto.Name = profile.User.Name;
                dto.Email = profile.User.GetEmailString();
            }
            dtos.Add(dto);
        }

        return dtos;
    }

    /// <summary>
    /// Deactivates a seller (admin only) - prevents them from selling
    /// </summary>
    public async Task<SellerProfileDto> DeactivateSellerAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var sellerProfile = await _sellerRepository.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile == null)
        {
            throw new SellerNotFoundException(sellerId);
        }

        var user = await _userRepository.GetByIdAsync(sellerId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(sellerId);
        }

        // Change user role from Seller to Student to deactivate selling
        if (user.IsInRole(UserRole.Seller))
        {
            user.ChangeRole(UserRole.Student);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        _logger.LogInformation("Seller deactivated: {SellerId}", sellerId);

        var dto = _mapper.Map<SellerProfileDto>(sellerProfile);
        dto.Name = user.Name;
        dto.Email = user.GetEmailString();
        return dto;
    }

    /// <summary>
    /// Creates a review for a seller from a completed order
    /// </summary>
    public async Task<SellerReviewDto> CreateReviewAsync(Guid customerId, CreateSellerReviewDto createDto, CancellationToken cancellationToken = default)
    {
        // Check if review already exists for this order and seller
        var existingReview = await _reviewRepository.GetByOrderAndSellerAsync(
            createDto.OrderId, createDto.SellerId, customerId, cancellationToken);

        if (existingReview != null)
        {
            throw new ValidationException("Review", $"Review already exists for order {createDto.OrderId} and seller {createDto.SellerId}");
        }

        var review = SellerReview.Create(
            createDto.SellerId,
            createDto.OrderId,
            customerId,
            createDto.Rating,
            createDto.Comment);

        var createdReview = await _reviewRepository.AddAsync(review, cancellationToken);

        _logger.LogInformation("Review created: {ReviewId}, Seller: {SellerId}, Order: {OrderId}, Rating: {Rating}",
            createdReview.ReviewId, createDto.SellerId, createDto.OrderId, createDto.Rating);

        // Recalculate seller rating based on all reviews
        await RecalculateSellerRatingAsync(createDto.SellerId, cancellationToken);

        return _mapper.Map<SellerReviewDto>(createdReview);
    }

    /// <summary>
    /// Gets all reviews for a seller
    /// </summary>
    public async Task<IEnumerable<SellerReviewDto>> GetSellerReviewsAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var reviews = await _reviewRepository.GetBySellerIdAsync(sellerId, cancellationToken);
        return _mapper.Map<IEnumerable<SellerReviewDto>>(reviews);
    }

    /// <summary>
    /// Updates an existing review
    /// </summary>
    public async Task<SellerReviewDto> UpdateReviewAsync(Guid reviewId, Guid customerId, UpdateSellerReviewDto updateDto, CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
        if (review == null)
        {
            throw new ValidationException("ReviewId", $"Review {reviewId} not found");
        }

        if (review.CustomerId != customerId)
        {
            throw new UnauthorizedException("You can only update your own reviews");
        }

        review.UpdateReview(updateDto.Rating, updateDto.Comment);
        var updatedReview = await _reviewRepository.UpdateAsync(review, cancellationToken);

        _logger.LogInformation("Review updated: {ReviewId}, Seller: {SellerId}, Rating: {Rating}",
            reviewId, review.SellerId, updateDto.Rating);

        // Recalculate seller rating based on all reviews
        await RecalculateSellerRatingAsync(review.SellerId, cancellationToken);

        return _mapper.Map<SellerReviewDto>(updatedReview);
    }

    private void PublishSellerCreatedEvent(SellerProfile sellerProfile, User user)
    {
        try
        {
            var sellerEvent = new SellerCreatedEventDto
            {
                SellerId = sellerProfile.SellerId,
                UserId = sellerProfile.SellerId,
                Email = user.GetEmailString(),
                Name = user.Name,
                Location = sellerProfile.Location,
                CreatedDate = sellerProfile.CreatedDate
            };

            _messageProducer.SendMessage(sellerEvent, "SellerCreated");
            _logger.LogInformation("SellerCreated event published: {SellerId}", sellerProfile.SellerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SellerCreated event: {SellerId}", sellerProfile.SellerId);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private void PublishSellerUpdatedEvent(SellerProfile sellerProfile)
    {
        try
        {
            var sellerEvent = new SellerUpdatedEventDto
            {
                SellerId = sellerProfile.SellerId,
                Rating = sellerProfile.Rating,
                TotalSales = sellerProfile.TotalSales,
                TotalBooksSold = sellerProfile.TotalBooksSold,
                Location = sellerProfile.Location,
                UpdatedDate = sellerProfile.UpdatedDate ?? DateTime.UtcNow
            };

            _messageProducer.SendMessage(sellerEvent, "SellerUpdated");
            _logger.LogInformation("SellerUpdated event published: {SellerId}", sellerProfile.SellerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SellerUpdated event: {SellerId}", sellerProfile.SellerId);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private void PublishBookAddedForSaleEvent(SellerBookListing listing)
    {
        try
        {
            var bookEvent = new BookAddedForSaleEventDto
            {
                ListingId = listing.ListingId,
                SellerId = listing.SellerId,
                BookISBN = listing.BookISBN,
                Price = listing.Price,
                Quantity = listing.Quantity,
                Condition = listing.Condition,
                CreatedDate = listing.CreatedDate
            };

            _messageProducer.SendMessage(bookEvent, "BookAddedForSale");
            _logger.LogInformation("BookAddedForSale event published: {ListingId}, ISBN: {ISBN}", 
                listing.ListingId, listing.BookISBN);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish BookAddedForSale event: {ListingId}", listing.ListingId);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private async Task PublishBookStockUpdatedEventAsync(string bookISBN, CancellationToken cancellationToken)
    {
        try
        {
            // Get all active listings for this book ISBN
            var allListings = await _listingRepository.GetByBookISBNAsync(bookISBN, cancellationToken);
            var activeListings = allListings
                .Where(l => l.IsActive && !l.IsSold && l.Quantity > 0)
                .ToList();

            // Calculate aggregated stock data
            var totalStock = activeListings.Sum(l => l.Quantity);
            var availableSellers = activeListings.Select(l => l.SellerId).Distinct().Count();
            var minPrice = activeListings.Any() ? activeListings.Min(l => l.Price) : 0m;
            var maxPrice = activeListings.Any() ? activeListings.Max(l => l.Price) : 0m;
            var averagePrice = activeListings.Any() ? activeListings.Average(l => l.Price) : 0m;

            // Create seller entries
            var sellers = activeListings.Select(l => new
            {
                SellerId = l.SellerId.ToString(),
                Price = l.Price,
                Quantity = l.Quantity,
                Condition = l.Condition,
                LastUpdated = l.UpdatedDate
            }).ToList();

            var bookStockUpdatedEvent = new
            {
                BookISBN = bookISBN,
                TotalStock = totalStock,
                AvailableSellers = availableSellers,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                AveragePrice = averagePrice,
                UpdatedAt = DateTime.UtcNow,
                Sellers = sellers
            };

            _messageProducer.SendMessage(bookStockUpdatedEvent, "BookStockUpdated");
            _logger.LogInformation("BookStockUpdated event published: ISBN: {ISBN}, TotalStock: {TotalStock}, AvailableSellers: {AvailableSellers}, MinPrice: {MinPrice}", 
                bookISBN, totalStock, availableSellers, minPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish BookStockUpdated event: {ISBN}", bookISBN);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private void PublishBookSoldEvent(SellerBookListing listing, string buyerId, Guid orderId, Guid orderItemId, int quantity)
    {
        try
        {
            var bookSoldEvent = new
            {
                ListingId = listing.ListingId,
                SellerId = listing.SellerId,
                BookISBN = listing.BookISBN,
                BuyerId = buyerId,
                OrderId = orderId,
                OrderItemId = orderItemId,
                Quantity = quantity,
                Price = listing.Price,
                Condition = listing.Condition,
                SoldDate = listing.SoldDate
            };

            _messageProducer.SendMessage(bookSoldEvent, "BookSold");
            _logger.LogInformation("BookSold event published: {ListingId}, ISBN: {ISBN}, BuyerId: {BuyerId}", 
                listing.ListingId, listing.BookISBN, buyerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish BookSold event: {ListingId}", listing.ListingId);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }
}

