using WarehouseService.Data;
using WarehouseService.Models;
using Microsoft.EntityFrameworkCore;

namespace WarehouseService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        try
        {
            Console.WriteLine("Seeding warehouse data...");
            
            context.ChangeTracker.Clear();

            var warehouseItems = LoadWarehouseItemsFromCsv();
            var existingItemIds = context.WarehouseItems.AsNoTracking().Select(w => w.Id).ToHashSet();
            var newItems = warehouseItems.Where(w => !existingItemIds.Contains(w.Id)).ToList();

            Console.WriteLine($"Found {existingItemIds.Count} existing warehouse items in database.");
            Console.WriteLine($"{newItems.Count} new items to seed.");

            if (newItems.Any())
            {
                await InsertWarehouseItemsWithTransactionAsync(context, newItems);
                Console.WriteLine($"Successfully seeded {newItems.Count} new warehouse items.");
            }
            else
            {
                Console.WriteLine("No new warehouse items to seed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static List<WarehouseItem> LoadWarehouseItemsFromCsv()
    {
        var warehouseItems = new List<WarehouseItem>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "WarehouseItems_Small.csv");

        Console.WriteLine($"=== CSV LOADING DEBUG ===");
        Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"Looking for CSV at: {csvPath}");
        Console.WriteLine($"File exists: {File.Exists(csvPath)}");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"ERROR: CSV file not found at: {csvPath}");
            
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (Directory.Exists(dataDir))
            {
                Console.WriteLine($"Files in Data directory:");
                foreach (var file in Directory.GetFiles(dataDir))
                {
                    Console.WriteLine($"  - {file}");
                }
            }
            else
            {
                Console.WriteLine($"Data directory does not exist at: {dataDir}");
            }
            
            return warehouseItems;
        }

        try
        {
            var allLines = File.ReadAllLines(csvPath);
            Console.WriteLine($"Total lines in CSV: {allLines.Length}");
            
            if (allLines.Length == 0)
            {
                Console.WriteLine("ERROR: CSV file is empty");
                return warehouseItems;
            }

            var header = allLines[0];
            Console.WriteLine($"CSV Header: {header}");

            int lineCount = 0;
            int successCount = 0;
            int errorCount = 0;
            
            for (int i = 1; i < allLines.Length; i++)
            {
                var line = allLines[i];
                lineCount++;
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var values = line.Split(',');

                if (lineCount <= 3)
                {
                    Console.WriteLine($"Line {lineCount}: {values.Length} columns - [{string.Join("|", values)}]");
                }

                if (values.Length >= 7)
                {
                    try
                    {
                        var item = new WarehouseItem
                        {
                            Id = int.Parse(values[0].Trim()),
                            BookISBN = values[1].Trim(),
                            SellerId = values[2].Trim(),
                            Quantity = int.Parse(values[3].Trim()),
                            Price = decimal.Parse(values[4].Trim()),
                            Location = values[5].Trim(),
                            IsNew = bool.Parse(values[6].Trim())
                        };
                        warehouseItems.Add(item);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errorCount <= 5)
                        {
                            Console.WriteLine($"ERROR parsing line {lineCount}: {ex.Message}");
                            Console.WriteLine($"Line content: {line}");
                        }
                    }
                }
                else
                {
                    errorCount++;
                    if (errorCount <= 5)
                    {
                        Console.WriteLine($"ERROR: Line {lineCount} has {values.Length} columns, expected 7");
                        Console.WriteLine($"Line content: {line}");
                    }
                }
            }

            Console.WriteLine($"=== CSV LOADING SUMMARY ===");
            Console.WriteLine($"Total lines processed: {lineCount}");
            Console.WriteLine($"Successfully loaded: {successCount}");
            Console.WriteLine($"Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR loading CSV: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return warehouseItems;
    }

    private static async Task InsertWarehouseItemsWithTransactionAsync(AppDbContext context, List<WarehouseItem> items)
    {
        const int batchSize = 1000;
        int totalInserted = 0;
        int totalSkipped = 0;

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT WarehouseItems ON");

            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = items.Skip(i).Take(batchSize).ToList();
                
                try
                {
                    var values = batch.Select(item =>
                        $"({item.Id}, '{EscapeSql(item.BookISBN)}', '{EscapeSql(item.SellerId)}', {item.Quantity}, {item.Price}, '{EscapeSql(item.Location)}', {(item.IsNew ? 1 : 0)})"
                    );
                    var sql = "INSERT INTO WarehouseItems (Id, BookISBN, SellerId, Quantity, Price, Location, IsNew) VALUES " + string.Join(",", values);

                    await context.Database.ExecuteSqlRawAsync(sql);
                    totalInserted += batch.Count;
                    Console.WriteLine($"Inserted batch of {batch.Count} items. Total: {totalInserted}/{items.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Batch insert failed, trying individual inserts for this batch. Error: {ex.Message}");
                    foreach (var item in batch)
                    {
                        try
                        {
                            var sql = $"INSERT INTO WarehouseItems (Id, BookISBN, SellerId, Quantity, Price, Location, IsNew) VALUES ({item.Id}, '{EscapeSql(item.BookISBN)}', '{EscapeSql(item.SellerId)}', {item.Quantity}, {item.Price}, '{EscapeSql(item.Location)}', {(item.IsNew ? 1 : 0)})";
                            await context.Database.ExecuteSqlRawAsync(sql);
                            totalInserted++;
                        }
                        catch
                        {
                            totalSkipped++;
                        }
                    }
                    Console.WriteLine($"Processed batch individually. Total inserted: {totalInserted}, Total skipped: {totalSkipped}");
                }
            }

            await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT WarehouseItems OFF");
            await transaction.CommitAsync();
            Console.WriteLine($"Final statistics - Inserted: {totalInserted}, Skipped (duplicates): {totalSkipped}");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Transaction failed: {ex.Message}");
            throw;
        }
    }

    private static string EscapeSql(string? value)
    {
        if (value == null) return string.Empty;
        return value.Replace("'", "''");
    }
}