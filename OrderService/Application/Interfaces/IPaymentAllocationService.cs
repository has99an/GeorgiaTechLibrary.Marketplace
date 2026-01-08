using OrderService.Application.DTOs;
using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

public interface IPaymentAllocationService
{
    Task<List<PaymentAllocation>> CreatePaymentAllocationsAsync(Order order, decimal platformFeePercentage);
    Task<List<PaymentAllocationDto>> GetPendingPayoutsAsync(string sellerId);
    Task<SellerSettlementDto> ProcessSettlementAsync(string sellerId, DateOnly periodStart, DateOnly periodEnd);
    Task<List<SellerSettlementDto>> GetSettlementHistoryAsync(string sellerId);
}
