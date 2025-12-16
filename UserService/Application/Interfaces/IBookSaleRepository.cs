using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

/// <summary>
/// Repository interface for BookSale entity
/// </summary>
public interface IBookSaleRepository
{
    Task<BookSale?> GetByIdAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookSale>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookSale>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookSale>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookSale>> GetByBuyerIdAsync(string buyerId, CancellationToken cancellationToken = default);
    Task<BookSale> AddAsync(BookSale sale, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid saleId, CancellationToken cancellationToken = default);
}


