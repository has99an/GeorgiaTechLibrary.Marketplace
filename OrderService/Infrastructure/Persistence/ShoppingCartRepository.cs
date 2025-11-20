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
            .AsNoTracking()
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
        // CRITICAL FIX: Always detach the cart and all its items first
        // This prevents EF Core from trying to update the tracked entity
        var entry = _context.Entry(cart);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
        
        // Detach all items
        foreach (var item in cart.Items)
        {
            var itemEntry = _context.Entry(item);
            if (itemEntry.State != EntityState.Detached)
            {
                itemEntry.State = EntityState.Detached;
            }
        }

        // Get cart ID before we lose reference
        var cartId = cart.ShoppingCartId;
        var updatedDate = cart.UpdatedDate;

        // Get currently existing CartItems from database
        var existingItems = await _context.CartItems
            .Where(ci => ci.ShoppingCartId == cartId)
            .ToListAsync();

        var existingItemsByKey = existingItems.ToDictionary(
            e => new { e.BookISBN, e.SellerId },
            e => e);

        // Build set of current cart items by key
        var currentItemsByKey = cart.Items.ToDictionary(
            item => new { item.BookISBN, item.SellerId },
            item => item);

        // DELETE items that exist in database but NOT in cart.Items (removed/cleared items)
        var itemsToDelete = existingItems
            .Where(ei => !currentItemsByKey.ContainsKey(new { ei.BookISBN, ei.SellerId }))
            .ToList();

        foreach (var itemToDelete in itemsToDelete)
        {
            _context.CartItems.Remove(itemToDelete);
        }

        // UPDATE or ADD items that are in cart.Items
        foreach (var item in cart.Items)
        {
            var key = new { item.BookISBN, item.SellerId };
            if (existingItemsByKey.TryGetValue(key, out var existingItem))
            {
                // Item exists - update quantity and price if changed
                // Note: existingItem is already tracked, so changes will be detected automatically
                if (existingItem.Quantity != item.Quantity)
                {
                    existingItem.UpdateQuantity(item.Quantity);
                }
                if (existingItem.UnitPrice.Amount != item.UnitPrice.Amount)
                {
                    existingItem.UpdatePrice(item.UnitPrice.Amount);
                }
            }
            else
            {
                // This is a new item - create it directly
                var newItem = CartItem.Create(item.BookISBN, item.SellerId, item.Quantity, item.UnitPrice.Amount);
                var newItemEntry = _context.Entry(newItem);
                newItemEntry.Property("ShoppingCartId").CurrentValue = cartId;
                _context.CartItems.Add(newItem);
            }
        }

        // Save all changes (deletions, updates, additions)
        await _context.SaveChangesAsync();

        // Update UpdatedDate using raw SQL
        if (updatedDate.HasValue)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE ShoppingCarts SET UpdatedDate = {0} WHERE ShoppingCartId = {1}",
                updatedDate.Value,
                cartId);
        }
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

