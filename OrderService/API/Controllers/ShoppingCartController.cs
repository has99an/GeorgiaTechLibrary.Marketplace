using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Services;
using OrderService.Domain.Exceptions;

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
    /// Converts the cart to an order (payment is processed first)
    /// </summary>
    [HttpPost("{customerId}/checkout")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Checkout(
        string customerId,
        [FromBody] CheckoutDto checkoutDto)
    {
        _logger.LogInformation("=== CHECKOUT REQUEST RECEIVED ===");
        _logger.LogInformation("STEP 1: Request received - CustomerId: {CustomerId}", customerId);
        _logger.LogInformation("STEP 1: Request Method: {Method}", Request.Method);
        _logger.LogInformation("STEP 1: Request Path: {Path}", Request.Path);
        _logger.LogInformation("STEP 1: Request Content-Type: {ContentType}", Request.ContentType ?? "NULL");
        _logger.LogInformation("STEP 1: Request ContentLength: {ContentLength}", Request.ContentLength?.ToString() ?? "NULL");
        _logger.LogInformation("STEP 1: Request HasFormContentType: {HasForm}", Request.HasFormContentType);
        _logger.LogInformation("STEP 1: Request Body.CanRead: {CanRead}, Body.CanSeek: {CanSeek}", Request.Body.CanRead, Request.Body.CanSeek);
        
        // Try to read request body if possible
        if (Request.ContentLength > 0)
        {
            try
            {
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var bodyContent = await reader.ReadToEndAsync();
                _logger.LogInformation("STEP 1: Request body content: {Body}", bodyContent);
                Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "STEP 1: Failed to read request body");
            }
        }
        else
        {
            _logger.LogWarning("STEP 1: Request has no body (ContentLength is 0 or null)");
        }
        
        _logger.LogInformation("STEP 2: ModelState.IsValid: {IsValid}", ModelState.IsValid);
        _logger.LogInformation("STEP 2: CheckoutDto is null: {IsNull}", checkoutDto == null);
        
        // Log all ModelState entries
        foreach (var key in ModelState.Keys)
        {
            var entry = ModelState[key];
            if (entry?.Errors.Count > 0)
            {
                _logger.LogWarning("STEP 2: ModelState error for key '{Key}': {Errors}", 
                    key, string.Join("; ", entry.Errors.Select(e => e.ErrorMessage)));
            }
            else
            {
                _logger.LogInformation("STEP 2: ModelState key '{Key}': {Value}", 
                    key, entry?.AttemptedValue ?? "null");
            }
        }

        if (!ModelState.IsValid)
        {
            var modelStateErrors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                .ToList();
            
            _logger.LogWarning("ModelState validation failed. Errors: {Errors}", 
                string.Join("; ", modelStateErrors));
            
            return BadRequest(new { 
                error = "Validation failed", 
                errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                )
            });
        }

        if (checkoutDto == null)
        {
            _logger.LogWarning("CheckoutDto is null after model binding");
            return BadRequest(new { error = "CheckoutDto is required", details = "Request body is missing or could not be deserialized" });
        }

        _logger.LogInformation("CheckoutDto received - Amount: {Amount}, PaymentMethod: {PaymentMethod}, DeliveryAddress is null: {AddressIsNull}",
            checkoutDto.Amount, checkoutDto.PaymentMethod, checkoutDto.DeliveryAddress == null);

        if (checkoutDto.DeliveryAddress == null)
        {
            _logger.LogWarning("DeliveryAddress is null in CheckoutDto");
            return BadRequest(new { error = "Delivery address is required", details = "DeliveryAddress field is missing or null" });
        }

        _logger.LogInformation("DeliveryAddress - Street: {Street}, City: {City}, PostalCode: {PostalCode}, State: {State}, Country: {Country}",
            checkoutDto.DeliveryAddress.Street, checkoutDto.DeliveryAddress.City, checkoutDto.DeliveryAddress.PostalCode,
            checkoutDto.DeliveryAddress.State, checkoutDto.DeliveryAddress.Country);

        try
        {
            var order = await _cartService.ConvertCartToOrderAsync(
                customerId, 
                checkoutDto.DeliveryAddress, 
                checkoutDto.Amount, 
                checkoutDto.PaymentMethod);
            
            _logger.LogInformation("Checkout successful - OrderId: {OrderId}", order.OrderId);
            return CreatedAtAction(
                nameof(OrdersController.GetOrder),
                "Orders",
                new { orderId = order.OrderId },
                order);
        }
        catch (ShoppingCartException ex)
        {
            _logger.LogWarning(ex, "Shopping cart error during checkout");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidPaymentException ex)
        {
            _logger.LogWarning(ex, "Payment validation error during checkout");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during checkout");
            return StatusCode(500, new { error = "An error occurred while processing checkout", details = ex.Message });
        }
    }
}

