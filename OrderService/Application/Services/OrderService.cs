using System.Net.Http;
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
        _logger.LogInformation("=== ORDER CREATION STARTED ===");
        _logger.LogInformation("CustomerId: {CustomerId}, OrderItems Count: {ItemCount}",
            createOrderDto.CustomerId, createOrderDto.OrderItems?.Count ?? 0);

        // Delivery address must be provided in DTO (no HTTP calls to UserService)
        if (createOrderDto.DeliveryAddress == null)
        {
            _logger.LogWarning("Step 1: FAILED - Delivery address is required");
            throw new ArgumentNullException(nameof(createOrderDto.DeliveryAddress), "Delivery address is required");
        }

        _logger.LogInformation("Step 1: Using delivery address from request DTO");
        var deliveryAddress = Address.Create(
            createOrderDto.DeliveryAddress.Street,
            createOrderDto.DeliveryAddress.City,
            createOrderDto.DeliveryAddress.PostalCode,
            createOrderDto.DeliveryAddress.State,
            createOrderDto.DeliveryAddress.Country);
        _logger.LogInformation("Step 1: Delivery address created from DTO - {Address}",
            deliveryAddress.GetFullAddress());

        // Validate that seller cannot buy their own books
        _logger.LogInformation("Step 1.5: Validating seller cannot buy own books...");
        var conflictingItems = createOrderDto.OrderItems
            .Where(item => item.SellerId.Equals(createOrderDto.CustomerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (conflictingItems.Any())
        {
            _logger.LogWarning("Step 1.5: FAILED - Seller {CustomerId} attempted to buy their own books", createOrderDto.CustomerId);
            var errorMessage = "Sellers cannot buy their own books";
            throw new ValidationException(errorMessage);
        }
        _logger.LogInformation("Step 1.5: SUCCESS - No seller self-purchase detected");

        _logger.LogInformation("Step 2: Creating order items from DTO...");
        var orderItems = createOrderDto.OrderItems
            .Select((item, index) =>
            {
                _logger.LogInformation("Step 2.{Index}: Creating order item - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}, UnitPrice: {UnitPrice}",
                    index + 1, item.BookISBN, item.SellerId, item.Quantity, item.UnitPrice);
                return OrderItem.Create(
                    item.BookISBN,
                    item.SellerId,
                    item.Quantity,
                    item.UnitPrice);
            })
            .ToList();
        _logger.LogInformation("Step 2: SUCCESS - Created {ItemCount} order items", orderItems.Count);

        _logger.LogInformation("Step 3: Creating Order entity...");
        var order = Order.Create(createOrderDto.CustomerId, orderItems, deliveryAddress);
        _logger.LogInformation("Step 3: SUCCESS - Order entity created - OrderId: {OrderId}, TotalAmount: {TotalAmount}",
            order.OrderId, order.TotalAmount.Amount);

        _logger.LogInformation("Step 4: Saving order to database...");
        var createdOrder = await _orderRepository.CreateAsync(order);
        _logger.LogInformation("Step 4: SUCCESS - Order saved to database - OrderId: {OrderId}", createdOrder.OrderId);

        _logger.LogInformation("Step 5: Publishing OrderCreated event to RabbitMQ...");
        _logger.LogInformation("Step 5: Order details - OrderId: {OrderId}, CustomerId: {CustomerId}, OrderItems: {ItemCount}",
            createdOrder.OrderId, createdOrder.CustomerId, createdOrder.OrderItems.Count);
        foreach (var item in createdOrder.OrderItems)
        {
            _logger.LogInformation("Step 5: OrderItem - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}",
                item.BookISBN, item.SellerId, item.Quantity);
        }

        await PublishOrderCreatedEventAsync(createdOrder);
        _logger.LogInformation("Step 5: SUCCESS - OrderCreated event published to RabbitMQ");

        _logger.LogInformation("=== ORDER CREATION COMPLETED ===");
        _logger.LogInformation("OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}",
            createdOrder.OrderId, createdOrder.CustomerId, createdOrder.TotalAmount.Amount);

        return MapToDto(createdOrder);
    }

    public async Task<OrderDto> CreateOrderWithPaymentAsync(CreateOrderDto createOrderDto, decimal paymentAmount, string transactionId)
    {
        _logger.LogInformation("=== ORDER CREATION WITH PAYMENT STARTED ===");
        _logger.LogInformation("CustomerId: {CustomerId}, PaymentAmount: {PaymentAmount}, TransactionId: {TransactionId}, OrderItems Count: {ItemCount}",
            createOrderDto.CustomerId, paymentAmount, transactionId, createOrderDto.OrderItems?.Count ?? 0);

        // Validate delivery address is provided (required for checkout)
        if (createOrderDto.DeliveryAddress == null)
        {
            _logger.LogWarning("Step 1: FAILED - Delivery address is required");
            throw new ArgumentNullException(nameof(createOrderDto.DeliveryAddress), "Delivery address is required");
        }

        _logger.LogInformation("Step 1: Using delivery address from request DTO");
        var deliveryAddress = Address.Create(
            createOrderDto.DeliveryAddress.Street,
            createOrderDto.DeliveryAddress.City,
            createOrderDto.DeliveryAddress.PostalCode,
            createOrderDto.DeliveryAddress.State,
            createOrderDto.DeliveryAddress.Country);
        _logger.LogInformation("Step 1: Delivery address created from DTO - {Address}",
            deliveryAddress.GetFullAddress());

        // Validate that seller cannot buy their own books
        _logger.LogInformation("Step 1.5: Validating seller cannot buy own books...");
        var conflictingItems = createOrderDto.OrderItems
            .Where(item => item.SellerId.Equals(createOrderDto.CustomerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (conflictingItems.Any())
        {
            _logger.LogWarning("Step 1.5: FAILED - Seller {CustomerId} attempted to buy their own books", createOrderDto.CustomerId);
            var errorMessage = "Sellers cannot buy their own books";
            throw new ValidationException(errorMessage);
        }
        _logger.LogInformation("Step 1.5: SUCCESS - No seller self-purchase detected");

        _logger.LogInformation("Step 2: Creating order items from DTO...");
        var orderItems = createOrderDto.OrderItems
            .Select((item, index) =>
            {
                _logger.LogInformation("Step 2.{Index}: Creating order item - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}, UnitPrice: {UnitPrice}",
                    index + 1, item.BookISBN, item.SellerId, item.Quantity, item.UnitPrice);
                return OrderItem.Create(
                    item.BookISBN,
                    item.SellerId,
                    item.Quantity,
                    item.UnitPrice);
            })
            .ToList();
        _logger.LogInformation("Step 2: SUCCESS - Created {ItemCount} order items", orderItems.Count);

        _logger.LogInformation("Step 3: Creating Order entity with Paid status...");
        var order = Order.CreatePaid(createOrderDto.CustomerId, orderItems, deliveryAddress, paymentAmount);
        _logger.LogInformation("Step 3: SUCCESS - Order entity created with Paid status - OrderId: {OrderId}, TotalAmount: {TotalAmount}",
            order.OrderId, order.TotalAmount.Amount);

        _logger.LogInformation("Step 4: Saving order to database...");
        var createdOrder = await _orderRepository.CreateAsync(order);
        _logger.LogInformation("Step 4: SUCCESS - Order saved to database - OrderId: {OrderId}", createdOrder.OrderId);

        _logger.LogInformation("Step 5: Publishing OrderCreated event to RabbitMQ (with Paid status)...");
        _logger.LogInformation("Step 5: Order details - OrderId: {OrderId}, CustomerId: {CustomerId}, Status: {Status}, OrderItems: {ItemCount}",
            createdOrder.OrderId, createdOrder.CustomerId, createdOrder.Status, createdOrder.OrderItems.Count);
        foreach (var item in createdOrder.OrderItems)
        {
            _logger.LogInformation("Step 5: OrderItem - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}",
                item.BookISBN, item.SellerId, item.Quantity);
        }

        await PublishOrderCreatedEventAsync(createdOrder);
        _logger.LogInformation("Step 5: SUCCESS - OrderCreated event published to RabbitMQ");

        // Mark all items as Processing
        foreach (var item in createdOrder.OrderItems)
        {
            item.MarkAsProcessing();
        }
        await _orderRepository.UpdateAsync(createdOrder);

        _logger.LogInformation("Step 6: Publishing OrderPaid event to RabbitMQ (for stock reduction)...");
        await PublishOrderPaidEventAsync(createdOrder);
        _logger.LogInformation("Step 6: SUCCESS - OrderPaid event published to RabbitMQ");

        _logger.LogInformation("=== ORDER CREATION WITH PAYMENT COMPLETED ===");
        _logger.LogInformation("OrderId: {OrderId}, CustomerId: {CustomerId}, Status: {Status}, TotalAmount: {TotalAmount}, PaidDate: {PaidDate}",
            createdOrder.OrderId, createdOrder.CustomerId, createdOrder.Status, createdOrder.TotalAmount.Amount, createdOrder.PaidDate);

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

        // If order is already paid, return it without processing payment again
        if (order.Status == OrderStatus.Paid)
        {
            _logger.LogInformation("Order {OrderId} is already paid. Returning existing order without processing payment again.", orderId);
            return MapToDto(order);
        }

        // Process payment through payment service
        var paymentResult = await _paymentService.ProcessPaymentAsync(
            orderId,
            payOrderDto.Amount,
            payOrderDto.PaymentMethod);

        if (!paymentResult.Success)
            throw new InvalidPaymentException($"Payment failed: {paymentResult.Message}");

        // Update order
        order.ProcessPayment(payOrderDto.Amount);
        
        // Mark all items as Processing
        foreach (var item in order.OrderItems)
        {
            item.MarkAsProcessing();
        }
        
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
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        return new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount?.Amount ?? 0,
            Status = order.Status.ToString(),
            PaidDate = order.PaidDate,
            ShippedDate = order.ShippedDate,
            DeliveredDate = order.DeliveredDate,
            CancelledDate = order.CancelledDate,
            RefundedDate = order.RefundedDate,
            CancellationReason = order.CancellationReason,
            RefundReason = order.RefundReason,
            DeliveryAddress = order.DeliveryAddress != null
                ? new AddressDto
                {
                    Street = order.DeliveryAddress.Street ?? string.Empty,
                    City = order.DeliveryAddress.City ?? string.Empty,
                    PostalCode = order.DeliveryAddress.PostalCode ?? string.Empty,
                    State = order.DeliveryAddress.State,
                    Country = order.DeliveryAddress.Country
                }
                : throw new InvalidOperationException($"Order {order.OrderId} has no delivery address configured"),
            OrderItems = order.OrderItems?.Select(item => new OrderItemDto
            {
                OrderItemId = item.OrderItemId,
                OrderId = item.OrderId,
                BookISBN = item.BookISBN ?? string.Empty,
                SellerId = item.SellerId ?? string.Empty,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice?.Amount ?? 0,
                Status = item.Status.ToString()
            }).ToList() ?? new List<OrderItemDto>()
        };
    }

    private async Task PublishOrderCreatedEventAsync(Order order)
    {
        _logger.LogInformation("=== PUBLISHING ORDERCREATED EVENT ===");
        _logger.LogInformation("Step 1: Creating OrderCreated event object...");

        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount.Amount,
            PaymentStatus = order.Status.ToString(), // "Paid" when created from checkout
            PaidDate = order.PaidDate,
            OrderItems = order.OrderItems.Select(item => new
            {
                OrderItemId = item.OrderItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount
            }).ToList()
        };

        _logger.LogInformation("Step 1: Event object created - OrderId: {OrderId}, PaymentStatus: {PaymentStatus}, OrderItems: {ItemCount}",
            orderEvent.OrderId, orderEvent.PaymentStatus, orderEvent.OrderItems.Count);

        _logger.LogInformation("Step 2: Serializing event to JSON...");
        var json = System.Text.Json.JsonSerializer.Serialize(orderEvent);
        _logger.LogInformation("Step 2: Event serialized - JSON length: {JsonLength}", json.Length);
        _logger.LogInformation("Step 2: JSON content: {Json}", json);

        _logger.LogInformation("Step 3: Sending message to RabbitMQ with routing key 'OrderCreated'...");
        try
        {
            await _messageProducer.SendMessageAsync(orderEvent, "OrderCreated");
            _logger.LogInformation("Step 3: SUCCESS - Message sent to RabbitMQ");
            _logger.LogInformation("=== ORDERCREATED EVENT PUBLISHED SUCCESSFULLY ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 3: FAILED - Error sending message to RabbitMQ");
            _logger.LogError("Error: {Error}", ex.Message);
            throw;
        }
    }

    private async Task PublishOrderPaidEventAsync(Order order)
    {
        _logger.LogInformation("=== PUBLISHING ORDERPAID EVENT ===");
        _logger.LogInformation("Step 1: Order details - OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}, OrderItems: {ItemCount}",
            order.OrderId, order.CustomerId, order.TotalAmount.Amount, order.OrderItems.Count);

        _logger.LogInformation("Step 2: Creating OrderPaid event object...");
        var orderEvent = new
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount.Amount,
            PaidDate = order.PaidDate,
            OrderItems = order.OrderItems.Select(item => new
            {
                OrderItemId = item.OrderItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount
            }).ToList()
        };

        _logger.LogInformation("Step 2: Event object created - OrderId: {OrderId}, OrderItems: {ItemCount}",
            orderEvent.OrderId, orderEvent.OrderItems.Count);
        foreach (var item in orderEvent.OrderItems)
        {
            _logger.LogInformation("Step 2: OrderItem - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}",
                item.BookISBN, item.SellerId, item.Quantity);
        }

        _logger.LogInformation("Step 3: Serializing event to JSON...");
        var json = System.Text.Json.JsonSerializer.Serialize(orderEvent);
        _logger.LogInformation("Step 3: Event serialized - JSON length: {JsonLength}", json.Length);
        _logger.LogInformation("Step 3: JSON content: {Json}", json);

        _logger.LogInformation("Step 4: Sending message to RabbitMQ with routing key 'OrderPaid'...");
        try
        {
            await _messageProducer.SendMessageAsync(orderEvent, "OrderPaid");
            _logger.LogInformation("Step 4: SUCCESS - Message sent to RabbitMQ");
            _logger.LogInformation("=== ORDERPAID EVENT PUBLISHED SUCCESSFULLY ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step 4: FAILED - Error sending message to RabbitMQ");
            _logger.LogError("Error: {Error}", ex.Message);
            throw;
        }
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

