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

    // NOTE: ConvertCartToOrderAsync has been replaced by the new checkout flow
    // Use CheckoutService.CreateCheckoutSessionAsync and CheckoutService.ConfirmPaymentAsync instead

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

