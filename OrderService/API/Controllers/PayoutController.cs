using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/sellers")]
public class PayoutController : ControllerBase
{
    private readonly IPaymentAllocationService _paymentAllocationService;
    private readonly ILogger<PayoutController> _logger;

    public PayoutController(
        IPaymentAllocationService paymentAllocationService,
        ILogger<PayoutController> logger)
    {
        _paymentAllocationService = paymentAllocationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets pending payouts for a specific seller
    /// </summary>
    [HttpGet("{sellerId}/payouts")]
    [ProducesResponseType(typeof(List<PaymentAllocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<PaymentAllocationDto>>> GetPendingPayouts(string sellerId)
    {
        _logger.LogInformation("Retrieving pending payouts for seller {SellerId}", sellerId);

        try
        {
            var payouts = await _paymentAllocationService.GetPendingPayoutsAsync(sellerId);
            
            _logger.LogInformation("Found {Count} pending payouts for seller {SellerId}", payouts.Count, sellerId);
            
            return Ok(payouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending payouts for seller {SellerId}", sellerId);
            return StatusCode(500, new { error = "An error occurred while retrieving payouts" });
        }
    }

    /// <summary>
    /// Gets settlement history for a specific seller
    /// </summary>
    [HttpGet("{sellerId}/settlements")]
    [ProducesResponseType(typeof(List<SellerSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SellerSettlementDto>>> GetSettlementHistory(string sellerId)
    {
        _logger.LogInformation("Retrieving settlement history for seller {SellerId}", sellerId);

        try
        {
            var settlements = await _paymentAllocationService.GetSettlementHistoryAsync(sellerId);
            
            _logger.LogInformation("Found {Count} settlements for seller {SellerId}", settlements.Count, sellerId);
            
            return Ok(settlements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving settlement history for seller {SellerId}", sellerId);
            return StatusCode(500, new { error = "An error occurred while retrieving settlements" });
        }
    }

    /// <summary>
    /// Processes a settlement for a seller for a specific period
    /// This would typically be called by an admin or background job
    /// </summary>
    [HttpPost("{sellerId}/settlements")]
    [ProducesResponseType(typeof(SellerSettlementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SellerSettlementDto>> ProcessSettlement(
        string sellerId,
        [FromQuery] DateOnly periodStart,
        [FromQuery] DateOnly periodEnd)
    {
        _logger.LogInformation("Processing settlement for seller {SellerId} for period {PeriodStart} to {PeriodEnd}",
            sellerId, periodStart, periodEnd);

        try
        {
            var settlement = await _paymentAllocationService.ProcessSettlementAsync(sellerId, periodStart, periodEnd);
            
            _logger.LogInformation("Settlement {SettlementId} created for seller {SellerId}", settlement.SettlementId, sellerId);
            
            return Created($"/api/sellers/{sellerId}/settlements/{settlement.SettlementId}", settlement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing settlement for seller {SellerId}", sellerId);
            return StatusCode(500, new { error = "An error occurred while processing settlement" });
        }
    }
}
