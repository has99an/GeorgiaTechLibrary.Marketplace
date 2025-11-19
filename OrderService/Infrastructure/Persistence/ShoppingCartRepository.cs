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
        _context.ShoppingCarts.Update(cart);
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

