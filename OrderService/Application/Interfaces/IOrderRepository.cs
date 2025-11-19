using OrderService.Domain.Entities;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Interfaces;

/// <summary>
/// Repository interface for Order aggregate
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId);
    Task<IEnumerable<Order>> GetByCustomerIdAsync(string customerId, int page = 1, int pageSize = 10);
    Task<IEnumerable<Order>> GetAllAsync(int page = 1, int pageSize = 10);
    Task<Order> CreateAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Guid orderId);
    Task<bool> ExistsAsync(Guid orderId);
    Task<int> GetTotalCountAsync();
    Task<int> GetCustomerOrderCountAsync(string customerId);
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10);
}

