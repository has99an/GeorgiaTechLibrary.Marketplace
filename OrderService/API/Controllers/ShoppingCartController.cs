using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Services;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShoppingCartController : ControllerBase
{
    private readonly IShoppingCartService _cartService;
    private readonly ILogger<ShoppingCartController> _logger;

    public ShoppingCartController(
        IShoppingCartService cartService,
        ILogger<ShoppingCartController> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the shopping cart for a customer
    /// </summary>
    [HttpGet("{customerId}")]
    [ProducesResponseType(typeof(ShoppingCartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShoppingCartDto>> GetCart(string customerId)
    {
        var cart = await _cartService.GetCartAsync(customerId);
        return Ok(cart);
    }

    /// <summary>
    /// Adds an item to the shopping cart
    /// </summary>
    [HttpPost("{customerId}/items")]
    [ProducesResponseType(typeof(ShoppingCartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShoppingCartDto>> AddItem(
        string customerId,
        [FromBody] AddToCartDto addToCartDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cart = await _cartService.AddItemAsync(customerId, addToCartDto);
        return Ok(cart);
    }

    /// <summary>
    /// Updates the quantity of an item in the cart
    /// </summary>
    [HttpPut("{customerId}/items/{cartItemId}")]
    [ProducesResponseType(typeof(ShoppingCartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShoppingCartDto>> UpdateItemQuantity(
        string customerId,
        Guid cartItemId,
        [FromBody] UpdateCartItemDto updateCartItemDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cart = await _cartService.UpdateItemQuantityAsync(customerId, cartItemId, updateCartItemDto);
        return Ok(cart);
    }

    /// <summary>
    /// Removes an item from the cart
    /// </summary>
    [HttpDelete("{customerId}/items/{cartItemId}")]
    [ProducesResponseType(typeof(ShoppingCartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShoppingCartDto>> RemoveItem(string customerId, Guid cartItemId)
    {
        var cart = await _cartService.RemoveItemAsync(customerId, cartItemId);
        return Ok(cart);
    }

    /// <summary>
    /// Clears all items from the cart
    /// </summary>
    [HttpDelete("{customerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearCart(string customerId)
    {
        await _cartService.ClearCartAsync(customerId);
        return NoContent();
    }

    /// <summary>
    /// Converts the cart to an order
    /// </summary>
    [HttpPost("{customerId}/checkout")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Checkout(
        string customerId,
        [FromBody] CheckoutDto? checkoutDto = null)
    {
        var order = await _cartService.ConvertCartToOrderAsync(customerId, checkoutDto?.DeliveryAddress);
        return CreatedAtAction(
            nameof(OrdersController.GetOrder),
            "Orders",
            new { orderId = order.OrderId },
            order);
    }
}

