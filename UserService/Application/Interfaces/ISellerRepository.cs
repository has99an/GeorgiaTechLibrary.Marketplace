using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

/// <summary>
/// Repository interface for SellerProfile entity
/// </summary>
public interface ISellerRepository
{
    Task<SellerProfile?> GetByIdAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<SellerProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SellerProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SellerProfile> AddAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default);
    Task<SellerProfile> UpdateAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid sellerId, CancellationToken cancellationToken = default);
}




