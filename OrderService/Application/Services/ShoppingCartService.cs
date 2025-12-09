using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Services;

/// <summary>
/// Application service for shopping cart operations
/// </summary>
public class ShoppingCartService : IShoppingCartService
{
    private readonly IShoppingCartRepository _cartRepository;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ShoppingCartService> _logger;

    public ShoppingCartService(
        IShoppingCartRepository cartRepository,
        IOrderService orderService,
        IPaymentService paymentService,
        ILogger<ShoppingCartService> logger)
    {
        _cartRepository = cartRepository;
        _orderService = orderService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<ShoppingCartDto> GetCartAsync(string customerId)
    {
        var cart = await _cartRepository.GetOrCreateForCustomerAsync(customerId);
        return MapToDto(cart);
    }

    public async Task<ShoppingCartDto> AddItemAsync(string customerId, AddToCartDto addToCartDto)
    {
        _logger.LogInformation("Adding item to cart for customer {CustomerId}", customerId);

        var cart = await _cartRepository.GetOrCreateForCustomerAsync(customerId);

        cart.AddItem(
            addToCartDto.BookISBN,
            addToCartDto.SellerId,
            addToCartDto.Quantity,
            addToCartDto.UnitPrice);

        await _cartRepository.UpdateAsync(cart);

        _logger.LogInformation("Item added to cart for customer {CustomerId}", customerId);

        return MapToDto(cart);
    }

    public async Task<ShoppingCartDto> UpdateItemQuantityAsync(
        string customerId,
        Guid cartItemId,
        UpdateCartItemDto updateCartItemDto)
    {
        _logger.LogInformation("Updating cart item {CartItemId} for customer {CustomerId}", cartItemId, customerId);

        var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
        if (cart == null)
            throw new ShoppingCartException($"Shopping cart not found for customer {customerId}");

        cart.UpdateItemQuantity(cartItemId, updateCartItemDto.Quantity);
        await _cartRepository.UpdateAsync(cart);

        _logger.LogInformation("Cart item {CartItemId} updated", cartItemId);

        return MapToDto(cart);
    }

    public async Task<ShoppingCartDto> RemoveItemAsync(string customerId, Guid cartItemId)
    {
        _logger.LogInformation("Removing cart item {CartItemId} for customer {CustomerId}", cartItemId, customerId);

        var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
        if (cart == null)
            throw new ShoppingCartException($"Shopping cart not found for customer {customerId}");

        cart.RemoveItem(cartItemId);
        await _cartRepository.UpdateAsync(cart);

        _logger.LogInformation("Cart item {CartItemId} removed", cartItemId);

        return MapToDto(cart);
    }

    public async Task ClearCartAsync(string customerId)
    {
        _logger.LogInformation("Clearing cart for customer {CustomerId}", customerId);

        var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
        if (cart == null)
            return;

        cart.Clear();
        await _cartRepository.UpdateAsync(cart);

        _logger.LogInformation("Cart cleared for customer {CustomerId}", customerId);
    }

    public async Task<OrderDto> ConvertCartToOrderAsync(string customerId, AddressDto deliveryAddress, decimal paymentAmount, string paymentMethod)
    {
        _logger.LogInformation("=== SHOPPING CART SERVICE: CHECKOUT STARTED ===");
        _logger.LogInformation("STEP 1: Input parameters - CustomerId: {CustomerId}, PaymentAmount: {PaymentAmount}, PaymentMethod: {PaymentMethod}", 
            customerId, paymentAmount, paymentMethod);
        _logger.LogInformation("STEP 1: DeliveryAddress is null: {IsNull}", deliveryAddress == null);
        if (deliveryAddress != null)
        {
            _logger.LogInformation("STEP 1: DeliveryAddress - Street: {Street}, City: {City}, PostalCode: {PostalCode}, State: {State}, Country: {Country}",
                deliveryAddress.Street, deliveryAddress.City, deliveryAddress.PostalCode, deliveryAddress.State, deliveryAddress.Country);
        }

        // Step 1: Validate cart
        _logger.LogInformation("STEP 2: Retrieving cart for customer {CustomerId}...", customerId);
        var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
        if (cart == null)
        {
            _logger.LogError("STEP 2: FAILED - Cart is null for customer {CustomerId}", customerId);
            throw new ShoppingCartException("Cannot create order from empty cart");
        }
        if (cart.IsEmpty())
        {
            _logger.LogError("STEP 2: FAILED - Cart is empty for customer {CustomerId}", customerId);
            throw new ShoppingCartException("Cannot create order from empty cart");
        }
        _logger.LogInformation("STEP 2: SUCCESS - Cart retrieved - CartId: {CartId}, ItemCount: {ItemCount}", 
            cart.ShoppingCartId, cart.Items.Count);

        var cartTotal = cart.CalculateTotal().Amount;
        _logger.LogInformation("STEP 3: Cart total: {CartTotal}, Payment amount: {PaymentAmount}", cartTotal, paymentAmount);

        // Step 2: Validate payment amount matches cart total
        _logger.LogInformation("STEP 4: Validating payment amount...");
        if (paymentAmount != cartTotal)
        {
            _logger.LogError("STEP 4: FAILED - Payment amount {PaymentAmount} does not match cart total {CartTotal}", paymentAmount, cartTotal);
            throw new InvalidPaymentException(cartTotal, paymentAmount);
        }
        _logger.LogInformation("STEP 4: SUCCESS - Payment amount matches cart total");

        // Step 3: Validate delivery address
        _logger.LogInformation("STEP 5: Validating delivery address...");
        if (deliveryAddress == null)
        {
            _logger.LogError("STEP 5: FAILED - Delivery address is null");
            throw new ArgumentNullException(nameof(deliveryAddress), "Delivery address is required");
        }

        _logger.LogInformation("STEP 5: Creating Address value object...");
        var address = Address.Create(
            deliveryAddress.Street,
            deliveryAddress.City,
            deliveryAddress.PostalCode,
            deliveryAddress.State,
            deliveryAddress.Country);
        _logger.LogInformation("STEP 5: SUCCESS - Delivery address validated: {Address}", address.GetFullAddress());

        // Step 4: Process payment FIRST (before creating order)
        _logger.LogInformation("STEP 6: Processing payment - Amount: {Amount}, Method: {Method}", paymentAmount, paymentMethod);
        
        // Generate a temporary order ID for payment processing
        var tempOrderId = Guid.NewGuid();
        _logger.LogInformation("STEP 6: Generated temporary OrderId for payment: {TempOrderId}", tempOrderId);
        
        _logger.LogInformation("STEP 6: Calling PaymentService.ProcessPaymentAsync...");
        var paymentResult = await _paymentService.ProcessPaymentAsync(tempOrderId, paymentAmount, paymentMethod);
        _logger.LogInformation("STEP 6: PaymentService returned - Success: {Success}, TransactionId: {TransactionId}, Message: {Message}",
            paymentResult.Success, paymentResult.TransactionId, paymentResult.Message);

        if (!paymentResult.Success)
        {
            _logger.LogError("STEP 6: FAILED - Payment failed: {Message}", paymentResult.Message);
            throw new InvalidPaymentException($"Payment failed: {paymentResult.Message}");
        }

        _logger.LogInformation("STEP 6: SUCCESS - Payment successful - TransactionId: {TransactionId}", paymentResult.TransactionId);

        // Step 5: Create order with Paid status (payment already processed)
        _logger.LogInformation("STEP 7: Creating CreateOrderDto...");
        _logger.LogInformation("STEP 7: Cart has {ItemCount} items", cart.Items.Count);
        foreach (var item in cart.Items)
        {
            _logger.LogInformation("STEP 7: Cart item - BookISBN: {BookISBN}, SellerId: {SellerId}, Quantity: {Quantity}, UnitPrice: {UnitPrice}",
                item.BookISBN, item.SellerId, item.Quantity, item.UnitPrice.Amount);
        }
        
        var createOrderDto = new CreateOrderDto
        {
            CustomerId = customerId,
            OrderItems = cart.Items.Select(item => new CreateOrderItemDto
            {
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount
            }).ToList(),
            DeliveryAddress = deliveryAddress
        };
        _logger.LogInformation("STEP 7: SUCCESS - CreateOrderDto created with {ItemCount} order items", createOrderDto.OrderItems.Count);

        // Create order with Paid status directly
        _logger.LogInformation("STEP 8: Calling OrderService.CreateOrderWithPaymentAsync...");
        _logger.LogInformation("STEP 8: Parameters - PaymentAmount: {PaymentAmount}, TransactionId: {TransactionId}",
            paymentAmount, paymentResult.TransactionId);
        var createdOrder = await _orderService.CreateOrderWithPaymentAsync(createOrderDto, paymentAmount, paymentResult.TransactionId);
        _logger.LogInformation("STEP 8: SUCCESS - Order created - OrderId: {OrderId}", createdOrder.OrderId);

        // Step 6: Clear the cart after successful order creation
        _logger.LogInformation("STEP 9: Clearing cart...");
        cart.Clear();
        await _cartRepository.UpdateAsync(cart);
        _logger.LogInformation("STEP 9: SUCCESS - Cart cleared");

        _logger.LogInformation("=== SHOPPING CART SERVICE: CHECKOUT COMPLETED ===");
        _logger.LogInformation("FINAL: Order {OrderId} created with Paid status for customer {CustomerId}", 
            createdOrder.OrderId, customerId);

        return createdOrder;
    }

    private ShoppingCartDto MapToDto(ShoppingCart cart)
    {
        return new ShoppingCartDto
        {
            ShoppingCartId = cart.ShoppingCartId,
            CustomerId = cart.CustomerId,
            CreatedDate = cart.CreatedDate,
            UpdatedDate = cart.UpdatedDate,
            TotalAmount = cart.CalculateTotal().Amount,
            ItemCount = cart.GetItemCount(),
            Items = cart.Items.Select(item => new CartItemDto
            {
                CartItemId = item.CartItemId,
                BookISBN = item.BookISBN,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount,
                TotalPrice = item.CalculateTotal().Amount,
                AddedDate = item.AddedDate,
                UpdatedDate = item.UpdatedDate
            }).ToList()
        };
    }
}

