using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Domain.Exceptions;
using OrderService.Infrastructure.Caching;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Application.Services;

public class CheckoutService : ICheckoutService
{
    private readonly IShoppingCartRepository _cartRepository;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentAllocationService _paymentAllocationService;
    private readonly IRedisCacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IShoppingCartRepository cartRepository,
        IOrderService orderService,
        IPaymentService paymentService,
        IPaymentAllocationService paymentAllocationService,
        IRedisCacheService cacheService,
        IConfiguration configuration,
        ILogger<CheckoutService> logger)
    {
        _cartRepository = cartRepository;
        _orderService = orderService;
        _paymentService = paymentService;
        _paymentAllocationService = paymentAllocationService;
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(string customerId, AddressDto deliveryAddress)
    {
        _logger.LogInformation("=== CHECKOUT SERVICE: CREATE SESSION STARTED ===");
        _logger.LogInformation("CustomerId: {CustomerId}", customerId);

        // Step 1: Validate and retrieve cart
        _logger.LogInformation("Step 1: Retrieving shopping cart...");
        var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
        if (cart == null || cart.IsEmpty())
        {
            _logger.LogError("Cart is empty for customer {CustomerId}", customerId);
            throw new ShoppingCartException("Cannot create checkout session from empty cart");
        }
        _logger.LogInformation("Step 1: SUCCESS - Cart retrieved with {ItemCount} items", cart.Items.Count);

        // Step 2: Validate delivery address
        if (deliveryAddress == null)
        {
            _logger.LogError("Delivery address is null");
            throw new ArgumentNullException(nameof(deliveryAddress), "Delivery address is required");
        }

        // Step 3: Get platform fee percentage from configuration
        var platformFeePercentage = decimal.Parse(_configuration["Payment:PlatformFeePercentage"] ?? "10");
        _logger.LogInformation("Step 2: Platform fee percentage: {PlatformFeePercentage}%", platformFeePercentage);

        // Step 4: Group items by seller and calculate fees
        _logger.LogInformation("Step 3: Grouping items by seller and calculating fees...");
        var itemsBySeller = new List<SellerAllocationDto>();
        var sellerGroups = cart.Items.GroupBy(item => item.SellerId);

        foreach (var sellerGroup in sellerGroups)
        {
            var sellerId = sellerGroup.Key;
            var sellerItems = sellerGroup.ToList();
            var sellerTotal = sellerItems.Sum(item => item.CalculateTotal().Amount);
            var platformFee = sellerTotal * (platformFeePercentage / 100m);
            var sellerPayout = sellerTotal - platformFee;

            var cartItemDtos = sellerItems.Select(item => new CartItemDto
            {
                CartItemId = item.CartItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount,
                AddedDate = item.AddedDate
            }).ToList();

            itemsBySeller.Add(new SellerAllocationDto
            {
                SellerId = sellerId,
                Items = cartItemDtos,
                SellerTotal = sellerTotal,
                PlatformFee = platformFee,
                SellerPayout = sellerPayout,
                PlatformFeePercentage = platformFeePercentage
            });

            _logger.LogInformation("Seller {SellerId}: Total={Total}, Fee={Fee}, Payout={Payout}",
                sellerId, sellerTotal, platformFee, sellerPayout);
        }

        var totalAmount = cart.CalculateTotal().Amount;
        _logger.LogInformation("Step 3: SUCCESS - Total amount: {TotalAmount}, Sellers: {SellerCount}",
            totalAmount, itemsBySeller.Count);

        // Step 5: Create checkout session
        var sessionId = Guid.NewGuid().ToString();
        var sessionExpiryMinutes = int.Parse(_configuration["Checkout:SessionExpiryMinutes"] ?? "30");
        var expiresAt = DateTime.UtcNow.AddMinutes(sessionExpiryMinutes);

        var session = new CheckoutSession
        {
            SessionId = sessionId,
            CustomerId = customerId,
            ShoppingCartId = cart.ShoppingCartId,
            TotalAmount = totalAmount,
            ItemsBySeller = itemsBySeller,
            DeliveryAddress = deliveryAddress,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        // Step 6: Store session in Redis
        _logger.LogInformation("Step 4: Storing session in Redis with {ExpiryMinutes} minutes expiry...", sessionExpiryMinutes);
        var cacheKey = $"checkout:session:{sessionId}";
        await _cacheService.SetAsync(cacheKey, session, TimeSpan.FromMinutes(sessionExpiryMinutes));
        _logger.LogInformation("Step 4: SUCCESS - Session stored with key: {CacheKey}", cacheKey);

        _logger.LogInformation("=== CHECKOUT SESSION CREATED ===");
        _logger.LogInformation("SessionId: {SessionId}, ExpiresAt: {ExpiresAt}", sessionId, expiresAt);

        return new CheckoutSessionDto
        {
            SessionId = sessionId,
            CustomerId = customerId,
            TotalAmount = totalAmount,
            ItemsBySeller = itemsBySeller,
            ExpiresAt = expiresAt,
            DeliveryAddress = deliveryAddress
        };
    }

    public async Task<OrderDto> ConfirmPaymentAsync(string sessionId, string paymentMethod)
    {
        _logger.LogInformation("=== CHECKOUT SERVICE: CONFIRM PAYMENT STARTED ===");
        _logger.LogInformation("SessionId: {SessionId}, PaymentMethod: {PaymentMethod}", sessionId, paymentMethod);

        // Step 1: Retrieve session from cache
        _logger.LogInformation("Step 1: Retrieving checkout session from Redis...");
        var cacheKey = $"checkout:session:{sessionId}";
        var session = await _cacheService.GetAsync<CheckoutSession>(cacheKey);

        if (session == null)
        {
            _logger.LogError("Checkout session not found or expired: {SessionId}", sessionId);
            throw new InvalidOperationException("Checkout session not found or expired. Please start checkout again.");
        }

        // Check if session has expired
        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _logger.LogError("Checkout session expired: {SessionId}, ExpiredAt: {ExpiresAt}", sessionId, session.ExpiresAt);
            await _cacheService.DeleteAsync(cacheKey);
            throw new InvalidOperationException("Checkout session has expired. Please start checkout again.");
        }

        _logger.LogInformation("Step 1: SUCCESS - Session retrieved, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}",
            session.CustomerId, session.TotalAmount);

        // Step 2: Process payment FIRST
        _logger.LogInformation("Step 2: Processing payment...");
        var tempOrderId = Guid.NewGuid();
        var paymentResult = await _paymentService.ProcessPaymentAsync(tempOrderId, session.TotalAmount, paymentMethod);

        if (!paymentResult.Success)
        {
            _logger.LogError("Payment failed: {Message}", paymentResult.Message);
            throw new InvalidPaymentException($"Payment failed: {paymentResult.Message}");
        }

        _logger.LogInformation("Step 2: SUCCESS - Payment processed, TransactionId: {TransactionId}", paymentResult.TransactionId);

        // Step 3: Create order with Paid status
        _logger.LogInformation("Step 3: Creating order from checkout session...");
        var createOrderDto = new CreateOrderDto
        {
            CustomerId = session.CustomerId,
            OrderItems = session.ItemsBySeller
                .SelectMany(seller => seller.Items.Select(item => new CreateOrderItemDto
                {
                    BookISBN = item.BookISBN,
                    SellerId = item.SellerId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }))
                .ToList(),
            DeliveryAddress = session.DeliveryAddress
        };

        var order = await _orderService.CreateOrderWithPaymentAsync(
            createOrderDto,
            session.TotalAmount,
            paymentResult.TransactionId ?? string.Empty);

        _logger.LogInformation("Step 3: SUCCESS - Order created: {OrderId}", order.OrderId);

        // Step 4: Clear the cart
        _logger.LogInformation("Step 4: Clearing shopping cart...");
        var cart = await _cartRepository.GetByIdAsync(session.ShoppingCartId);
        if (cart != null)
        {
            cart.Clear();
            await _cartRepository.UpdateAsync(cart);
            _logger.LogInformation("Step 4: SUCCESS - Cart cleared");
        }

        // Step 5: Delete checkout session from cache
        _logger.LogInformation("Step 5: Deleting checkout session from cache...");
        await _cacheService.DeleteAsync(cacheKey);
        _logger.LogInformation("Step 5: SUCCESS - Session deleted");

        _logger.LogInformation("=== CHECKOUT COMPLETED ===");
        _logger.LogInformation("OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}",
            order.OrderId, order.CustomerId, order.TotalAmount);

        return order;
    }

    public async Task<CheckoutSessionDto?> GetCheckoutSessionAsync(string sessionId)
    {
        _logger.LogInformation("Retrieving checkout session: {SessionId}", sessionId);

        var cacheKey = $"checkout:session:{sessionId}";
        var session = await _cacheService.GetAsync<CheckoutSession>(cacheKey);

        if (session == null)
        {
            _logger.LogWarning("Checkout session not found: {SessionId}", sessionId);
            return null;
        }

        // Check if expired
        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _logger.LogWarning("Checkout session expired: {SessionId}", sessionId);
            await _cacheService.DeleteAsync(cacheKey);
            return null;
        }

        return new CheckoutSessionDto
        {
            SessionId = session.SessionId,
            CustomerId = session.CustomerId,
            TotalAmount = session.TotalAmount,
            ItemsBySeller = session.ItemsBySeller,
            ExpiresAt = session.ExpiresAt,
            DeliveryAddress = session.DeliveryAddress
        };
    }
}
