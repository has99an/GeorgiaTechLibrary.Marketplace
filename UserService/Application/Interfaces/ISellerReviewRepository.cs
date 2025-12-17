using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

/// <summary>
/// Repository interface for SellerReview entity
/// </summary>
public interface ISellerReviewRepository
{
    Task<SellerReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default);
    Task<SellerReview?> GetByOrderAndSellerAsync(Guid orderId, Guid sellerId, Guid customerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SellerReview>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SellerReview>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<decimal> GetAverageRatingAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<int> GetReviewCountAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<SellerReview> AddAsync(SellerReview review, CancellationToken cancellationToken = default);
    Task<SellerReview> UpdateAsync(SellerReview review, CancellationToken cancellationToken = default);
}




