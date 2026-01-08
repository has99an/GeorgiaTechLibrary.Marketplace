using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence;

public interface ISellerSettlementRepository
{
    Task<SellerSettlement> CreateAsync(SellerSettlement settlement);
    Task<SellerSettlement?> GetByIdAsync(Guid settlementId);
    Task<List<SellerSettlement>> GetBySellerIdAsync(string sellerId);
    Task<SellerSettlement?> GetBySellerAndPeriodAsync(string sellerId, DateOnly periodStart, DateOnly periodEnd);
    Task UpdateAsync(SellerSettlement settlement);
}

public class SellerSettlementRepository : ISellerSettlementRepository
{
    private readonly AppDbContext _context;

    public SellerSettlementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SellerSettlement> CreateAsync(SellerSettlement settlement)
    {
        await _context.SellerSettlements.AddAsync(settlement);
        await _context.SaveChangesAsync();
        return settlement;
    }

    public async Task<SellerSettlement?> GetByIdAsync(Guid settlementId)
    {
        return await _context.SellerSettlements
            .FirstOrDefaultAsync(ss => ss.SettlementId == settlementId);
    }

    public async Task<List<SellerSettlement>> GetBySellerIdAsync(string sellerId)
    {
        return await _context.SellerSettlements
            .Where(ss => ss.SellerId == sellerId)
            .OrderByDescending(ss => ss.PeriodStart)
            .ToListAsync();
    }

    public async Task<SellerSettlement?> GetBySellerAndPeriodAsync(string sellerId, DateOnly periodStart, DateOnly periodEnd)
    {
        return await _context.SellerSettlements
            .FirstOrDefaultAsync(ss => ss.SellerId == sellerId 
                && ss.PeriodStart == periodStart 
                && ss.PeriodEnd == periodEnd);
    }

    public async Task UpdateAsync(SellerSettlement settlement)
    {
        _context.SellerSettlements.Update(settlement);
        await _context.SaveChangesAsync();
    }
}
