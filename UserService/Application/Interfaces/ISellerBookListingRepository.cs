using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

/// <summary>
/// Repository interface for SellerBookListing entity
/// </summary>
public interface ISellerBookListingRepository
{
    Task<SellerBookListing?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SellerBookListing>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SellerBookListing>> GetByBookISBNAsync(string bookISBN, CancellationToken cancellationToken = default);
    Task<SellerBookListing?> GetBySellerAndBookAsync(Guid sellerId, string bookISBN, string condition, CancellationToken cancellationToken = default);
    Task<SellerBookListing> AddAsync(SellerBookListing listing, CancellationToken cancellationToken = default);
    Task<SellerBookListing> UpdateAsync(SellerBookListing listing, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken = default);
}




