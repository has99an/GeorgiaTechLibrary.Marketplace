using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Services;

/// <summary>
/// Application service for order operations
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IMessageProducer messageProducer,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _paymentService = paymentService;
        _inventoryService = inventoryService;
        _messageProducer = messageProducer;
        _logger = logger;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", createOrderDto.CustomerId);

        // Create order items
        var orderItems = createOrderDto.OrderItems
            .Select(item => OrderItem.Create(
                item.BookISBN,
                item.SellerId,
                item.Quantity,
                item.UnitPrice))
            .ToList();

        // Create order
        var order = Order.Create(createOrderDto.CustomerId, orderItems);

        // Save order
        var createdOrder = await _orderRepository.CreateAsync(order);

        // Publish OrderCreated event
        await PublishOrderCreatedEventAsync(createdOrder);

        _logger.LogInformation("Order {OrderId} created successfully", createdOrder.OrderId);

        return MapToDto(createdOrder);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        return order != null ? MapToDto(order) : null;
    }

    public async Task<PagedResultDto<OrderDto>> GetOrdersByCustomerIdAsync(string customerId, int page = 1, int pageSize = 10)
    {
        var orders = await _orderRepository.GetByCustomerIdAsync(customerId, page, pageSize);
        var totalCount = await _orderRepository.GetCustomerOrderCountAsync(customerId);

        return new PagedResultDto<OrderDto>
        {
            Items = orders.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<OrderDto>> GetAllOrdersAsync(int page = 1, int pageSize = 10)
    {
        var orders = await _orderRepository.GetAllAsync(page, pageSize);
        var totalCount = await _orderRepository.GetTotalCountAsync();

        return new PagedResultDto<OrderDto>
        {
            Items = orders.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderDto> PayOrderAsync(Guid orderId, PayOrderDto payOrderDto)
    {
        _logger.LogInformation("Processing payment for order {OrderId}", orderId);

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new OrderNotFoundException(orderId);

        // Process payment through payment service
        var paymentResult = await _paymentService.ProcessPaymentAsync(
            orderId,
            payOrderDto.Amount,
            payOrderDto.PaymentMethod);

        if (!paymentResult.Success)
            throw new InvalidPaymentException($"Payment failed: {paymentResult.Message}");

        // Update order
        order.ProcessPayment(payOrderDto.Amount);
        await _orderRepository.UpdateAsync(order);

        // Publish OrderPaid event
        await PublishOrderPaidEventAsync(order);

        _logger.LogInformation("Order {OrderId} paid successfully", orderId);

        return MapToDto(order);
    }

    public async Task<OrderDto> ShipOrderAsync(Guid orderId)
    {
        _logger.LogInformation("Shipping order {OrderId}", orderId);

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new OrderNotFoundException(orderId);

        order.MarkAsShipped();
        await _orderRepository.UpdateAsync(order);

        // Publish OrderShipped event
        await PublishOrderShippedEventAsync(order);

        _logger.LogInformation("Order {OrderId} marked as shipped", orderId);

        return MapToDto(order);
    }

    public async Task<OrderDto> DeliverOrderAsync(Guid orderId)
    {
        _logger.LogInformation("Delivering order {OrderId}", orderId);

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new OrderNotFoundException(orderId);

        order.MarkAsDelivered();
        await _orderRepository.UpdateAsync(order);

        // Publish OrderDelivered event
        await PublishOrderDeliveredEventAsync(order);

        _logger.LogInformation("Order {OrderId} marked as delivered", orderId);

        return MapToDto(order);
    }

    public async Task<OrderDto> CancelOrderAsync(Guid orderId, CancelOrderDto cancelOrderDto)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new OrderNotFoundException(orderId);

        order.Cancel(cancelOrderDto.Reason);
        await _orderRepository.UpdateAsync(order);

        // Release inventory
        foreach (var item in order.OrderItems)
        {
            await _inventoryService.ReleaseInventoryAsync(
                orderId,
                item.BookISBN,
                item.SellerId,
                item.Quantity);
        }

        // Publish OrderCancelled event
        await PublishOrderCancelledEventAsync(order);

        _logger.LogInformation("Order {OrderId} cancelled", orderId);

        return MapToDto(order);
    }

    public async Task<OrderDto> RefundOrderAsync(Guid orderId, RefundOrderDto refundOrderDto)
    {
        _logger.LogInformation("Processing refund for order {OrderId}", orderId);

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new OrderNotFoundException(orderId);

        // Process refund through payment service
        var refundResult = await _paymentService.ProcessRefundAsync(
            orderId,
            order.TotalAmount.Amount,
            refundOrderDto.Reason);

        if (!refundResult.Success)
            throw new InvalidPaymentException($"Refund failed: {refundResult.Message}");

        // Update order
        order.ProcessRefund(refundOrderDto.Reason);
        await _orderRepository.UpdateAsync(order);

        // Restore inventory
        foreach (var item in order.OrderItems)
        {
            await _inventoryService.RestoreInventoryAsync(
                orderId,
                item.BookISBN,
                item.SellerId,
                item.Quantity);
        }

        // Publish OrderRefunded event
        await PublishOrderRefundedEventAsync(order);

        _logger.LogInformation("Order {OrderId} refunded", orderId);

        return MapToDto(order);
    }

    public async Task<PagedResultDto<OrderDto>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10)
    {
        var orders = await _orderRepository.GetOrdersByStatusAsync(status, page, pageSize);
        var totalCount = await _orderRepository.GetTotalCountAsync();

        return new PagedResultDto<OrderDto>
        {
            Items = orders.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount.Amount,
            Status = order.Status.ToString(),
            PaidDate = order.PaidDate,
            ShippedDate = order.ShippedDate,
            DeliveredDate = order.DeliveredDate,
            CancelledDate = order.CancelledDate,
            RefundedDate = order.RefundedDate,
            CancellationReason = order.CancellationReason,
            RefundReason = order.RefundReason,
            OrderItems = order.OrderItems.Select(item => new OrderItemDto
            {
                OrderItemId = item.OrderItemId,
                OrderId = item.OrderId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount,
                Status = item.Status
            }).ToList()
        };
    }

    private async Task PublishOrderCreatedEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount.Amount,
            OrderItems = order.OrderItems.Select(item => new
            {
                OrderItemId = item.OrderItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount
            }).ToList()
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderCreated");
    }

    private async Task PublishOrderPaidEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount.Amount,
            PaidDate = order.PaidDate
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderPaid");
    }

    private async Task PublishOrderShippedEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            ShippedDate = order.ShippedDate
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderShipped");
    }

    private async Task PublishOrderDeliveredEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            DeliveredDate = order.DeliveredDate
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderDelivered");
    }

    private async Task PublishOrderCancelledEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            CancelledDate = order.CancelledDate,
            Reason = order.CancellationReason,
            OrderItems = order.OrderItems.Select(item => new
            {
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity
            }).ToList()
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderCancelled");
    }

    private async Task PublishOrderRefundedEventAsync(Order order)
    {
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            RefundedDate = order.RefundedDate,
            TotalAmount = order.TotalAmount.Amount,
            Reason = order.RefundReason,
            OrderItems = order.OrderItems.Select(item => new
            {
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity
            }).ToList()
        };

        await _messageProducer.SendMessageAsync(orderEvent, "OrderRefunded");
    }
}

