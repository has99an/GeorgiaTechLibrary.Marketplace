using OrderService.Models;

namespace OrderService.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId);
    Task<Order> CreateOrderAsync(Order order);
    Task UpdateOrderAsync(Order order);
    Task DeleteOrderAsync(Guid orderId);
    Task<bool> OrderExistsAsync(Guid orderId);
    Task<IEnumerable<Order>> GetAllOrdersAsync();
}
