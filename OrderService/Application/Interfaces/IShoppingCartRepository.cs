using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

/// <summary>
/// Repository interface for ShoppingCart aggregate
/// </summary>
public interface IShoppingCartRepository
{
    Task<ShoppingCart?> GetByIdAsync(Guid shoppingCartId);
    Task<ShoppingCart?> GetByCustomerIdAsync(string customerId);
    Task<ShoppingCart> CreateAsync(ShoppingCart cart);
    Task UpdateAsync(ShoppingCart cart);
    Task DeleteAsync(Guid shoppingCartId);
    Task<bool> ExistsAsync(Guid shoppingCartId);
    Task<ShoppingCart> GetOrCreateForCustomerAsync(string customerId);
}

