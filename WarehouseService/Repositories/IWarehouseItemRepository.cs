using WarehouseService.Models;

namespace WarehouseService.Repositories;

public interface IWarehouseItemRepository
{
    Task<IEnumerable<WarehouseItem>> GetAllWarehouseItemsAsync();
    Task<WarehouseItem?> GetWarehouseItemByIdAsync(int id);
    Task<IEnumerable<WarehouseItem>> GetWarehouseItemsByBookIsbnAsync(string bookIsbn);
    Task<WarehouseItem> AddWarehouseItemAsync(WarehouseItem item);
    Task<WarehouseItem?> UpdateWarehouseItemAsync(int id, WarehouseItem item);
    Task<bool> DeleteWarehouseItemAsync(int id);
    Task<bool> WarehouseItemExistsAsync(int id);
    Task<WarehouseItem?> GetWarehouseItemByBookAndSellerAsync(string bookIsbn, string sellerId);
}
