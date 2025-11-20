using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence;

public class ShoppingCartRepository : IShoppingCartRepository
{
    private readonly AppDbContext _context;

    public ShoppingCartRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShoppingCart?> GetByIdAsync(Guid shoppingCartId)
    {
        return await _context.ShoppingCarts
            .Include(sc => sc.Items)
            .FirstOrDefaultAsync(sc => sc.ShoppingCartId == shoppingCartId);
    }

    public async Task<ShoppingCart?> GetByCustomerIdAsync(string customerId)
    {
        return await _context.ShoppingCarts
            .Include(sc => sc.Items)
            .FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
    }

    public async Task<ShoppingCart> CreateAsync(ShoppingCart cart)
    {
        _context.ShoppingCarts.Add(cart);
        await _context.SaveChangesAsync();
        return cart;
    }

    public async Task UpdateAsync(ShoppingCart cart)
    {
        var entry = _context.Entry(cart);
        
        if (entry.State == EntityState.Detached)
        {
            // Entity is not tracked, use Update to track it and all its children
            _context.ShoppingCarts.Update(cart);
        }
        else
        {
            // Entity is already tracked, just mark it as modified
            entry.State = EntityState.Modified;
            
            // Ensure all CartItems are properly tracked
            foreach (var item in cart.Items)
            {
                var itemEntry = _context.Entry(item);
                if (itemEntry.State == EntityState.Detached)
                {
                    // New item, add it
                    _context.CartItems.Add(item);
                }
                else
                {
                    // Existing item, mark as modified
                    itemEntry.State = EntityState.Modified;
                }
            }
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid shoppingCartId)
    {
        var cart = await GetByIdAsync(shoppingCartId);
        if (cart != null)
        {
            _context.ShoppingCarts.Remove(cart);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid shoppingCartId)
    {
        return await _context.ShoppingCarts.AnyAsync(sc => sc.ShoppingCartId == shoppingCartId);
    }

    public async Task<ShoppingCart> GetOrCreateForCustomerAsync(string customerId)
    {
        var cart = await GetByCustomerIdAsync(customerId);
        
        if (cart == null)
        {
            cart = ShoppingCart.Create(customerId);
            await CreateAsync(cart);
        }

        return cart;
    }
}

