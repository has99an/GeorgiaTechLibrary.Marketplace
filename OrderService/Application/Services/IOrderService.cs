using OrderService.Application.DTOs;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Services;

/// <summary>
/// Application service interface for order operations
/// </summary>
public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto);
    Task<OrderDto> CreateOrderWithPaymentAsync(CreateOrderDto createOrderDto, decimal paymentAmount, string transactionId);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId);
    Task<PagedResultDto<OrderDto>> GetOrdersByCustomerIdAsync(string customerId, int page = 1, int pageSize = 10);
    Task<PagedResultDto<OrderDto>> GetAllOrdersAsync(int page = 1, int pageSize = 10);
    Task<OrderDto> PayOrderAsync(Guid orderId, PayOrderDto payOrderDto);
    Task<OrderDto> ShipOrderAsync(Guid orderId);
    Task<OrderDto> DeliverOrderAsync(Guid orderId);
    Task<OrderDto> CancelOrderAsync(Guid orderId, CancelOrderDto cancelOrderDto);
    Task<OrderDto> RefundOrderAsync(Guid orderId, RefundOrderDto refundOrderDto);
    Task<PagedResultDto<OrderDto>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10);
}

