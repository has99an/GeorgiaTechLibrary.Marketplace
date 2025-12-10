using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for SellerReview entity
/// </summary>
public class SellerReviewRepository : ISellerReviewRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SellerReviewRepository> _logger;

    public SellerReviewRepository(AppDbContext context, ILogger<SellerReviewRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SellerReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerReviews
            .Include(r => r.SellerProfile)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId, cancellationToken);
    }

    public async Task<SellerReview?> GetByOrderAndSellerAsync(Guid orderId, Guid sellerId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerReviews
            .Include(r => r.SellerProfile)
            .FirstOrDefaultAsync(r => r.OrderId == orderId && r.SellerId == sellerId && r.CustomerId == customerId, cancellationToken);
    }

    public async Task<IEnumerable<SellerReview>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerReviews
            .Include(r => r.SellerProfile)
            .Where(r => r.SellerId == sellerId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SellerReview>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerReviews
            .Include(r => r.SellerProfile)
            .Where(r => r.OrderId == orderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetAverageRatingAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var average = await _context.SellerReviews
            .Where(r => r.SellerId == sellerId)
            .AverageAsync(r => (double?)r.Rating, cancellationToken);

        return average.HasValue ? (decimal)average.Value : 0.0m;
    }

    public async Task<int> GetReviewCountAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerReviews
            .Where(r => r.SellerId == sellerId)
            .CountAsync(cancellationToken);
    }

    public async Task<SellerReview> AddAsync(SellerReview review, CancellationToken cancellationToken = default)
    {
        await _context.SellerReviews.AddAsync(review, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async Task<SellerReview> UpdateAsync(SellerReview review, CancellationToken cancellationToken = default)
    {
        _context.SellerReviews.Update(review);
        await _context.SaveChangesAsync(cancellationToken);
        return review;
    }
}

