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
            
            // FJERN alle checks - bare kør seeding!
            context.WarehouseItems.RemoveRange(context.WarehouseItems);
            await context.SaveChangesAsync();
            
            var warehouseItems = LoadWarehouseItemsFromCsv();
            
            if (warehouseItems.Count > 0)
            {
                Console.WriteLine($"Inserting {warehouseItems.Count} items using bulk insert...");
                
                // Use a transaction to ensure IDENTITY_INSERT stays on
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // Enable IDENTITY_INSERT
                    await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT WarehouseItems ON");
                    
                    // Insert in smaller batches to avoid timeout
                    int batchSize = 1000;
                    for (int i = 0; i < warehouseItems.Count; i += batchSize)
                    {
                        var batch = warehouseItems.Skip(i).Take(batchSize).ToList();
                        await context.WarehouseItems.AddRangeAsync(batch);
                        await context.SaveChangesAsync();
                        Console.WriteLine($"Inserted batch {i / batchSize + 1}: {batch.Count} items");
                    }
                    
                    // Disable IDENTITY_INSERT
                    await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT WarehouseItems OFF");
                    
                    await transaction.CommitAsync();
                    Console.WriteLine($"Successfully seeded {warehouseItems.Count} warehouse items.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Transaction failed: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine("No warehouse items to seed.");
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
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "WarehouseItems_Updated.csv");

        Console.WriteLine($"=== CSV LOADING DEBUG ===");
        Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"Looking for CSV at: {csvPath}");
        Console.WriteLine($"File exists: {File.Exists(csvPath)}");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"ERROR: CSV file not found at: {csvPath}");
            
            // Try to list what files ARE in the Data directory
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

            // Skip header row
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

                // Debug first 3 lines
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
                        if (errorCount <= 5) // Only log first 5 errors
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
}
