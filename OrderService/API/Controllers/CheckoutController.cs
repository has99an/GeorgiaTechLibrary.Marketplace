using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Exceptions;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICheckoutService checkoutService,
        ILogger<CheckoutController> logger)
    {
        _checkoutService = checkoutService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a checkout session from the customer's shopping cart
    /// Groups items by seller and calculates platform fees
    /// </summary>
    [HttpPost("session")]
    [ProducesResponseType(typeof(CheckoutSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CheckoutSessionDto>> CreateCheckoutSession(
        [FromQuery] string customerId,
        [FromBody] AddressDto deliveryAddress)
    {
        _logger.LogInformation("Creating checkout session for customer {CustomerId}", customerId);

        try
        {
            var session = await _checkoutService.CreateCheckoutSessionAsync(customerId, deliveryAddress);
            
            _logger.LogInformation("Checkout session created: {SessionId}", session.SessionId);
            
            return CreatedAtAction(
                nameof(GetCheckoutSession),
                new { sessionId = session.SessionId },
                session);
        }
        catch (ShoppingCartException ex)
        {
            _logger.LogWarning(ex, "Shopping cart error during checkout session creation");
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex, "Validation error during checkout session creation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during checkout session creation");
            return StatusCode(500, new { error = "An error occurred while creating checkout session", details = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves an existing checkout session by ID
    /// </summary>
    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(typeof(CheckoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<CheckoutSessionDto>> GetCheckoutSession(string sessionId)
    {
        _logger.LogInformation("Retrieving checkout session: {SessionId}", sessionId);

        try
        {
            var session = await _checkoutService.GetCheckoutSessionAsync(sessionId);

            if (session == null)
            {
                _logger.LogWarning("Checkout session not found or expired: {SessionId}", sessionId);
                return StatusCode(410, new { error = "Checkout session has expired. Please start checkout again." });
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkout session");
            return StatusCode(500, new { error = "An error occurred while retrieving checkout session" });
        }
    }

    /// <summary>
    /// Confirms payment and creates the order
    /// Order is only created after successful payment processing
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderDto>> ConfirmPayment([FromBody] ConfirmPaymentDto confirmPaymentDto)
    {
        _logger.LogInformation("Confirming payment for session {SessionId}", confirmPaymentDto.SessionId);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var order = await _checkoutService.ConfirmPaymentAsync(
                confirmPaymentDto.SessionId,
                confirmPaymentDto.PaymentMethod);

            _logger.LogInformation("Payment confirmed and order created: {OrderId}", order.OrderId);

            return CreatedAtAction(
                nameof(OrdersController.GetOrder),
                "Orders",
                new { orderId = order.OrderId },
                order);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("expired"))
        {
            _logger.LogWarning(ex, "Checkout session not found or expired");
            return StatusCode(410, new { error = ex.Message });
        }
        catch (InvalidPaymentException ex)
        {
            _logger.LogWarning(ex, "Payment processing failed");
            return BadRequest(new { error = ex.Message, retryable = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment confirmation");
            return StatusCode(500, new { error = "An error occurred while processing payment", details = ex.Message });
        }
    }
}
