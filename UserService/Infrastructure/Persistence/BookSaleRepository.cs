using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for BookSale entity
/// </summary>
public class BookSaleRepository : IBookSaleRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<BookSaleRepository> _logger;

    public BookSaleRepository(AppDbContext context, ILogger<BookSaleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BookSale?> GetByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales
            .Include(s => s.Listing)
            .Include(s => s.SellerProfile)
            .FirstOrDefaultAsync(s => s.SaleId == saleId, cancellationToken);
    }

    public async Task<IEnumerable<BookSale>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales
            .Include(s => s.Listing)
            .Include(s => s.SellerProfile)
            .Where(s => s.ListingId == listingId)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BookSale>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales
            .Include(s => s.Listing)
            .Include(s => s.SellerProfile)
            .Where(s => s.SellerId == sellerId)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BookSale>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales
            .Include(s => s.Listing)
            .Include(s => s.SellerProfile)
            .Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BookSale>> GetByBuyerIdAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales
            .Include(s => s.Listing)
            .Include(s => s.SellerProfile)
            .Where(s => s.BuyerId == buyerId)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<BookSale> AddAsync(BookSale sale, CancellationToken cancellationToken = default)
    {
        await _context.BookSales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<bool> ExistsAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        return await _context.BookSales.AnyAsync(s => s.SaleId == saleId, cancellationToken);
    }
}

