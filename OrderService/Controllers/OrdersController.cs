using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Repositories;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderRepository orderRepository,
        IMessageProducer messageProducer,
        ILogger<OrdersController> logger)
    {
        _orderRepository = orderRepository;
        _messageProducer = messageProducer;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderDto createOrderDto)
    {
        try
        {
            // Calculate total amount
            decimal totalAmount = createOrderDto.OrderItems.Sum(item => item.Quantity * item.UnitPrice);

            // Create order
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerId = createOrderDto.CustomerId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                Status = "Pending",
                OrderItems = createOrderDto.OrderItems.Select(item => new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    BookISBN = item.BookISBN,
                    SellerId = item.SellerId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Status = "Pending"
                }).ToList()
            };

            var createdOrder = await _orderRepository.CreateOrderAsync(order);

            // Send OrderCreated event
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = createdOrder.OrderId,
                CustomerId = createdOrder.CustomerId,
                OrderDate = createdOrder.OrderDate,
                TotalAmount = createdOrder.TotalAmount,
                OrderItems = createdOrder.OrderItems.Select(item => new OrderItemEvent
                {
                    OrderItemId = item.OrderItemId,
                    BookISBN = item.BookISBN,
                    SellerId = item.SellerId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };

            _messageProducer.SendMessage(orderCreatedEvent, "OrderCreated");

            _logger.LogInformation("Order created with ID {OrderId}", createdOrder.OrderId);

            // Map to DTO and return
            var orderDto = new OrderDto
            {
                OrderId = createdOrder.OrderId,
                CustomerId = createdOrder.CustomerId,
                OrderDate = createdOrder.OrderDate,
                TotalAmount = createdOrder.TotalAmount,
                Status = createdOrder.Status,
                OrderItems = createdOrder.OrderItems.Select(item => new OrderItemDto
                {
                    OrderItemId = item.OrderItemId,
                    OrderId = item.OrderId,
                    BookISBN = item.BookISBN,
                    SellerId = item.SellerId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Status = item.Status
                }).ToList()
            };

            return CreatedAtAction(nameof(GetOrder), new { orderId = orderDto.OrderId }, orderDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid orderId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);

        if (order == null)
        {
            return NotFound();
        }

        var orderDto = new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            OrderItems = order.OrderItems.Select(item => new OrderItemDto
            {
                OrderItemId = item.OrderItemId,
                OrderId = item.OrderId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Status = item.Status
            }).ToList()
        };

        return Ok(orderDto);
    }

    [HttpPost("{orderId}/pay")]
    public async Task<IActionResult> PayOrder(Guid orderId, PayOrderDto payOrderDto)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);

        if (order == null)
        {
            return NotFound();
        }

        if (order.Status != "Pending")
        {
            return BadRequest("Order is not in a payable state");
        }

        if (payOrderDto.Amount != order.TotalAmount)
        {
            return BadRequest("Payment amount does not match order total");
        }

        // Update order status
        order.Status = "Paid";
        await _orderRepository.UpdateOrderAsync(order);

        // Send OrderPaid event
        var orderPaidEvent = new OrderPaidEvent
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            PaidDate = DateTime.UtcNow
        };

        _messageProducer.SendMessage(orderPaidEvent, "OrderPaid");

        _logger.LogInformation("Order {OrderId} marked as paid", orderId);

        return Ok(new { message = "Order paid successfully" });
    }
}
