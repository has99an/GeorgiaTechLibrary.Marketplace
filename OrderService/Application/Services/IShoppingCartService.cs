using OrderService.Application.DTOs;

namespace OrderService.Application.Services;

/// <summary>
/// Application service interface for shopping cart operations
/// </summary>
public interface IShoppingCartService
{
    Task<ShoppingCartDto> GetCartAsync(string customerId);
    Task<ShoppingCartDto> AddItemAsync(string customerId, AddToCartDto addToCartDto);
    Task<ShoppingCartDto> UpdateItemQuantityAsync(string customerId, Guid cartItemId, UpdateCartItemDto updateCartItemDto);
    Task<ShoppingCartDto> RemoveItemAsync(string customerId, Guid cartItemId);
    Task ClearCartAsync(string customerId);
    Task<OrderDto> ConvertCartToOrderAsync(string customerId);
}

