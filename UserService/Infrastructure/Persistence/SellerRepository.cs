using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for SellerProfile entity
/// </summary>
public class SellerRepository : ISellerRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SellerRepository> _logger;

    public SellerRepository(AppDbContext context, ILogger<SellerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SellerProfile?> GetByIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.SellerId == sellerId, cancellationToken);
    }

    public async Task<SellerProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.SellerId == userId, cancellationToken);
    }

    public async Task<IEnumerable<SellerProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SellerProfiles
            .Include(sp => sp.User)
            .OrderBy(sp => sp.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<SellerProfile> AddAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default)
    {
        await _context.SellerProfiles.AddAsync(sellerProfile, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sellerProfile;
    }

    public async Task<SellerProfile> UpdateAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default)
    {
        _context.SellerProfiles.Update(sellerProfile);
        await _context.SaveChangesAsync(cancellationToken);
        return sellerProfile;
    }

    public async Task<bool> ExistsAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return await _context.SellerProfiles.AnyAsync(sp => sp.SellerId == sellerId, cancellationToken);
    }
}






