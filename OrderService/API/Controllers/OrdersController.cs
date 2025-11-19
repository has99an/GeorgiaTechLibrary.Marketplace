using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Services;
using OrderService.Domain.ValueObjects;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await _orderService.CreateOrderAsync(createOrderDto);
        return CreatedAtAction(nameof(GetOrder), new { orderId = order.OrderId }, order);
    }

    /// <summary>
    /// Gets an order by ID
    /// </summary>
    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    /// <summary>
    /// Gets all orders for a customer (paginated)
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(PagedResultDto<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<OrderDto>>> GetCustomerOrders(
        string customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var orders = await _orderService.GetOrdersByCustomerIdAsync(customerId, page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Gets all orders (paginated, admin only)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<OrderDto>>> GetAllOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var orders = await _orderService.GetAllOrdersAsync(page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Gets orders by status (paginated)
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(PagedResultDto<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<OrderDto>>> GetOrdersByStatus(
        string status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            return BadRequest($"Invalid status: {status}");

        var orders = await _orderService.GetOrdersByStatusAsync(orderStatus, page, pageSize);
        return Ok(orders);
    }

    /// <summary>
    /// Processes payment for an order
    /// </summary>
    [HttpPost("{orderId}/pay")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> PayOrder(Guid orderId, [FromBody] PayOrderDto payOrderDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await _orderService.PayOrderAsync(orderId, payOrderDto);
        return Ok(order);
    }

    /// <summary>
    /// Marks an order as shipped
    /// </summary>
    [HttpPost("{orderId}/ship")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> ShipOrder(Guid orderId)
    {
        var order = await _orderService.ShipOrderAsync(orderId);
        return Ok(order);
    }

    /// <summary>
    /// Marks an order as delivered
    /// </summary>
    [HttpPost("{orderId}/deliver")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> DeliverOrder(Guid orderId)
    {
        var order = await _orderService.DeliverOrderAsync(orderId);
        return Ok(order);
    }

    /// <summary>
    /// Cancels an order
    /// </summary>
    [HttpPost("{orderId}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> CancelOrder(Guid orderId, [FromBody] CancelOrderDto cancelOrderDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await _orderService.CancelOrderAsync(orderId, cancelOrderDto);
        return Ok(order);
    }

    /// <summary>
    /// Processes a refund for an order
    /// </summary>
    [HttpPost("{orderId}/refund")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> RefundOrder(Guid orderId, [FromBody] RefundOrderDto refundOrderDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await _orderService.RefundOrderAsync(orderId, refundOrderDto);
        return Ok(order);
    }
}

