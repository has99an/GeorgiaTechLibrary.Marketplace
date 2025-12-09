using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for SellerBookListing entity
/// </summary>
public class SellerBookListingRepository : ISellerBookListingRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SellerBookListingRepository> _logger;

    public SellerBookListingRepository(AppDbContext context, ILogger<SellerBookListingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SellerBookListing?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerBookListings
            .Include(l => l.SellerProfile)
            .FirstOrDefaultAsync(l => l.ListingId == listingId, cancellationToken);
    }

    public async Task<IEnumerable<SellerBookListing>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerBookListings
            .Where(l => l.SellerId == sellerId)
            .OrderByDescending(l => l.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SellerBookListing>> GetByBookISBNAsync(string bookISBN, CancellationToken cancellationToken = default)
    {
        return await _context.SellerBookListings
            .Where(l => l.BookISBN == bookISBN && l.IsActive)
            .OrderBy(l => l.Price)
            .ToListAsync(cancellationToken);
    }

    public async Task<SellerBookListing?> GetBySellerAndBookAsync(Guid sellerId, string bookISBN, string condition, CancellationToken cancellationToken = default)
    {
        return await _context.SellerBookListings
            .FirstOrDefaultAsync(l => l.SellerId == sellerId && l.BookISBN == bookISBN && l.Condition == condition, cancellationToken);
    }

    public async Task<SellerBookListing> AddAsync(SellerBookListing listing, CancellationToken cancellationToken = default)
    {
        await _context.SellerBookListings.AddAsync(listing, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return listing;
    }

    public async Task<SellerBookListing> UpdateAsync(SellerBookListing listing, CancellationToken cancellationToken = default)
    {
        _context.SellerBookListings.Update(listing);
        await _context.SaveChangesAsync(cancellationToken);
        return listing;
    }

    public async Task<bool> DeleteAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetByIdAsync(listingId);
        if (listing == null)
        {
            return false;
        }

        _context.SellerBookListings.Remove(listing);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerBookListings.AnyAsync(l => l.ListingId == listingId, cancellationToken);
    }
}

