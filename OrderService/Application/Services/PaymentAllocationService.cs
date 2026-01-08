using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Application.Services;

public class PaymentAllocationService : IPaymentAllocationService
{
    private readonly IPaymentAllocationRepository _allocationRepository;
    private readonly ISellerSettlementRepository _settlementRepository;
    private readonly ILogger<PaymentAllocationService> _logger;

    public PaymentAllocationService(
        IPaymentAllocationRepository allocationRepository,
        ISellerSettlementRepository settlementRepository,
        ILogger<PaymentAllocationService> logger)
    {
        _allocationRepository = allocationRepository;
        _settlementRepository = settlementRepository;
        _logger = logger;
    }

    public async Task<List<PaymentAllocation>> CreatePaymentAllocationsAsync(Order order, decimal platformFeePercentage)
    {
        _logger.LogInformation("Creating payment allocations for order {OrderId} with platform fee {PlatformFeePercentage}%",
            order.OrderId, platformFeePercentage);

        var allocations = new List<PaymentAllocation>();

        // Group order items by seller
        var sellerGroups = order.OrderItems.GroupBy(item => item.SellerId);

        foreach (var sellerGroup in sellerGroups)
        {
            var sellerId = sellerGroup.Key;
            var sellerTotal = sellerGroup.Sum(item => item.CalculateTotal().Amount);

            _logger.LogInformation("Creating allocation for seller {SellerId}: Total amount {TotalAmount}",
                sellerId, sellerTotal);

            var allocation = PaymentAllocation.Create(
                order.OrderId,
                sellerId,
                sellerTotal,
                platformFeePercentage);

            var createdAllocation = await _allocationRepository.CreateAsync(allocation);
            allocations.Add(createdAllocation);

            _logger.LogInformation("Created allocation {AllocationId} - Seller payout: {SellerPayout}, Platform fee: {PlatformFee}",
                createdAllocation.AllocationId,
                createdAllocation.SellerPayout.Amount,
                createdAllocation.PlatformFee.Amount);
        }

        _logger.LogInformation("Created {Count} payment allocations for order {OrderId}",
            allocations.Count, order.OrderId);

        return allocations;
    }

    public async Task<List<PaymentAllocationDto>> GetPendingPayoutsAsync(string sellerId)
    {
        _logger.LogInformation("Retrieving pending payouts for seller {SellerId}", sellerId);

        var allocations = await _allocationRepository.GetPendingBySellerIdAsync(sellerId);

        return allocations.Select(MapToDto).ToList();
    }

    public async Task<SellerSettlementDto> ProcessSettlementAsync(string sellerId, DateOnly periodStart, DateOnly periodEnd)
    {
        _logger.LogInformation("Processing settlement for seller {SellerId} for period {PeriodStart} to {PeriodEnd}",
            sellerId, periodStart, periodEnd);

        // Check if settlement already exists for this period
        var existingSettlement = await _settlementRepository.GetBySellerAndPeriodAsync(sellerId, periodStart, periodEnd);
        if (existingSettlement != null)
        {
            _logger.LogWarning("Settlement already exists for seller {SellerId} for period {PeriodStart} to {PeriodEnd}",
                sellerId, periodStart, periodEnd);
            return MapSettlementToDto(existingSettlement);
        }

        // Get all pending allocations for this seller
        var pendingAllocations = await _allocationRepository.GetPendingBySellerIdAsync(sellerId);

        // Filter allocations within the settlement period
        var periodAllocations = pendingAllocations
            .Where(a => DateOnly.FromDateTime(a.CreatedAt) >= periodStart 
                     && DateOnly.FromDateTime(a.CreatedAt) <= periodEnd)
            .ToList();

        if (!periodAllocations.Any())
        {
            _logger.LogInformation("No pending allocations found for seller {SellerId} in period {PeriodStart} to {PeriodEnd}",
                sellerId, periodStart, periodEnd);
            
            // Create zero-amount settlement
            var zeroSettlement = SellerSettlement.Create(sellerId, periodStart, periodEnd, 0);
            var createdZeroSettlement = await _settlementRepository.CreateAsync(zeroSettlement);
            return MapSettlementToDto(createdZeroSettlement);
        }

        // Calculate total payout
        var totalPayout = periodAllocations.Sum(a => a.SellerPayout.Amount);

        _logger.LogInformation("Creating settlement for seller {SellerId}: {Count} allocations, Total payout: {TotalPayout}",
            sellerId, periodAllocations.Count, totalPayout);

        // Create settlement record
        var settlement = SellerSettlement.Create(sellerId, periodStart, periodEnd, totalPayout);
        var createdSettlement = await _settlementRepository.CreateAsync(settlement);

        // Mark allocations as paid out
        foreach (var allocation in periodAllocations)
        {
            allocation.MarkAsPaidOut();
            await _allocationRepository.UpdateAsync(allocation);
        }

        _logger.LogInformation("Settlement {SettlementId} created and {Count} allocations marked as paid out",
            createdSettlement.SettlementId, periodAllocations.Count);

        return MapSettlementToDto(createdSettlement);
    }

    public async Task<List<SellerSettlementDto>> GetSettlementHistoryAsync(string sellerId)
    {
        _logger.LogInformation("Retrieving settlement history for seller {SellerId}", sellerId);

        var settlements = await _settlementRepository.GetBySellerIdAsync(sellerId);

        return settlements.Select(MapSettlementToDto).ToList();
    }

    private PaymentAllocationDto MapToDto(PaymentAllocation allocation)
    {
        return new PaymentAllocationDto
        {
            AllocationId = allocation.AllocationId,
            OrderId = allocation.OrderId,
            SellerId = allocation.SellerId,
            TotalAmount = allocation.TotalAmount.Amount,
            PlatformFee = allocation.PlatformFee.Amount,
            SellerPayout = allocation.SellerPayout.Amount,
            Status = allocation.Status.ToString(),
            CreatedAt = allocation.CreatedAt,
            PaidOutAt = allocation.PaidOutAt
        };
    }

    private SellerSettlementDto MapSettlementToDto(SellerSettlement settlement)
    {
        return new SellerSettlementDto
        {
            SettlementId = settlement.SettlementId,
            SellerId = settlement.SellerId,
            PeriodStart = settlement.PeriodStart,
            PeriodEnd = settlement.PeriodEnd,
            TotalPayout = settlement.TotalPayout.Amount,
            Status = settlement.Status.ToString(),
            CreatedAt = settlement.CreatedAt,
            ProcessedAt = settlement.ProcessedAt
        };
    }
}
