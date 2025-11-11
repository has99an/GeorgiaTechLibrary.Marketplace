using WarehouseService.Data;
using WarehouseService.Models;
using Microsoft.EntityFrameworkCore;

namespace WarehouseService.Repositories;

public class WarehouseItemRepository : IWarehouseItemRepository
{
    private readonly AppDbContext _context;

    public WarehouseItemRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WarehouseItem>> GetAllWarehouseItemsAsync()
    {
        return await _context.WarehouseItems.ToListAsync();
    }

    public async Task<WarehouseItem?> GetWarehouseItemByIdAsync(int id)
    {
        return await _context.WarehouseItems.FindAsync(id);
    }

    public async Task<IEnumerable<WarehouseItem>> GetWarehouseItemsByBookIsbnAsync(string bookIsbn)
    {
        return await _context.WarehouseItems
            .Where(w => w.BookISBN == bookIsbn)
            .ToListAsync();
    }

    public async Task<WarehouseItem> AddWarehouseItemAsync(WarehouseItem item)
    {
        _context.WarehouseItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<WarehouseItem?> UpdateWarehouseItemAsync(int id, WarehouseItem item)
    {
        var existingItem = await _context.WarehouseItems.FindAsync(id);
        if (existingItem == null)
        {
            return null;
        }

        existingItem.Quantity = item.Quantity;
        existingItem.Price = item.Price;
        existingItem.Condition = item.Condition;

        await _context.SaveChangesAsync();
        return existingItem;
    }

    public async Task<bool> DeleteWarehouseItemAsync(int id)
    {
        var item = await _context.WarehouseItems.FindAsync(id);
        if (item == null)
        {
            return false;
        }

        _context.WarehouseItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> WarehouseItemExistsAsync(int id)
    {
        return await _context.WarehouseItems.AnyAsync(w => w.Id == id);
    }

    public async Task<WarehouseItem?> GetWarehouseItemByBookAndSellerAsync(string bookIsbn, string sellerId)
    {
        return await _context.WarehouseItems
            .FirstOrDefaultAsync(w => w.BookISBN == bookIsbn && w.SellerId == sellerId);
    }
}
