using WarehouseService.Models;
using Microsoft.EntityFrameworkCore;

namespace WarehouseService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        context.ChangeTracker.Clear();

        // Check if data already exists
        if (context.WarehouseItems.Any())
        {
            Console.WriteLine("Warehouse items already exist. Skipping seed data.");
            return;
        }

        var warehouseItems = new List<WarehouseItem>
        {
            new WarehouseItem
            {
                BookISBN = "0439136350", // Harry Potter example
                SellerId = "GeorgiaTechLibrary",
                Quantity = 50,
                Price = 15.99m,
                Condition = "New"
            },
            new WarehouseItem
            {
                BookISBN = "0439136350", // Same book, different seller
                SellerId = "student123",
                Quantity = 10,
                Price = 12.50m,
                Condition = "Used"
            },
            new WarehouseItem
            {
                BookISBN = "0061122416", // Different book
                SellerId = "GeorgiaTechLibrary",
                Quantity = 25,
                Price = 18.99m,
                Condition = "New"
            }
        };

        try
        {
            await context.WarehouseItems.AddRangeAsync(warehouseItems);
            await context.SaveChangesAsync();
            Console.WriteLine($"Successfully seeded {warehouseItems.Count} warehouse items.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding warehouse items: {ex.Message}");
        }
    }
}
