using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence;

public interface IPaymentAllocationRepository
{
    Task<PaymentAllocation> CreateAsync(PaymentAllocation allocation);
    Task<PaymentAllocation?> GetByIdAsync(Guid allocationId);
    Task<List<PaymentAllocation>> GetByOrderIdAsync(Guid orderId);
    Task<List<PaymentAllocation>> GetBySellerIdAsync(string sellerId);
    Task<List<PaymentAllocation>> GetPendingBySellerIdAsync(string sellerId);
    Task UpdateAsync(PaymentAllocation allocation);
}

public class PaymentAllocationRepository : IPaymentAllocationRepository
{
    private readonly AppDbContext _context;

    public PaymentAllocationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentAllocation> CreateAsync(PaymentAllocation allocation)
    {
        await _context.PaymentAllocations.AddAsync(allocation);
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task<PaymentAllocation?> GetByIdAsync(Guid allocationId)
    {
        return await _context.PaymentAllocations
            .Include(pa => pa.Order)
            .FirstOrDefaultAsync(pa => pa.AllocationId == allocationId);
    }

    public async Task<List<PaymentAllocation>> GetByOrderIdAsync(Guid orderId)
    {
        return await _context.PaymentAllocations
            .Where(pa => pa.OrderId == orderId)
            .ToListAsync();
    }

    public async Task<List<PaymentAllocation>> GetBySellerIdAsync(string sellerId)
    {
        return await _context.PaymentAllocations
            .Where(pa => pa.SellerId == sellerId)
            .OrderByDescending(pa => pa.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PaymentAllocation>> GetPendingBySellerIdAsync(string sellerId)
    {
        return await _context.PaymentAllocations
            .Where(pa => pa.SellerId == sellerId && pa.Status == PaymentAllocationStatus.Pending)
            .OrderBy(pa => pa.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(PaymentAllocation allocation)
    {
        _context.PaymentAllocations.Update(allocation);
        await _context.SaveChangesAsync();
    }
}
