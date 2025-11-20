using BookService.Models;
using CsvHelper;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace BookService.Data
{
    public static class SeedData
    {
        public static async Task Initialize(AppDbContext context)
        {
            context.ChangeTracker.Clear();

            var books = ReadBooksFromCsv();
            var existingIsbns = context.Books.AsNoTracking().Select(b => b.ISBN).ToHashSet();
            var newBooks = books.Where(b => !existingIsbns.Contains(b.ISBN)).ToList();

            Console.WriteLine($"Found {existingIsbns.Count} existing books in database.");
            Console.WriteLine($"{newBooks.Count} new books to seed.");

            if (newBooks.Any())
            {
                try
                {
                    await InsertBooksWithRawSqlAsync(context, newBooks);
                    Console.WriteLine($"Successfully seeded {newBooks.Count} new books to database using raw SQL.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error seeding books to database: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("No new books to seed.");
            }
        }

        private static List<Book> ReadBooksFromCsv()
        {
            var books = new List<Book>();
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Books_Small.csv");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"CSV file not found at: {csvPath}");
                return books;
            }

            try
            {
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                csv.Read();
                csv.ReadHeader();

                int booksRead = 0;
                while (csv.Read())
                {
                    try
                    {
                        var book = new Book
                        {
                            ISBN = csv.GetField("ISBN"),
                            BookTitle = csv.GetField("Book-Title"), 
                            BookAuthor = csv.GetField("Book-Author"),
                            YearOfPublication = int.TryParse(csv.GetField("Year-Of-Publication"), out int year) ? year : 0,
                            Publisher = csv.GetField("Publisher"),
                            ImageUrlS = csv.GetField("Image-URL-S"),
                            ImageUrlM = csv.GetField("Image-URL-M"), 
                            ImageUrlL = csv.GetField("Image-URL-L"),
                            Genre = csv.GetField("Genre"),
                            Language = csv.GetField("Language"),
                            PageCount = int.TryParse(csv.GetField("PageCount"), out int pages) ? pages : 0,
                            Description = "", // CSV har ikke Description felt
                            Rating = double.TryParse(csv.GetField("Rating"), out double rating) ? rating : 0.0,
                            AvailabilityStatus = csv.GetField("AvailabilityStatus"), 
                            Edition = "", // CSV har ikke Edition felt
                            Format = csv.GetField("Format")
                        };

                        if (!string.IsNullOrEmpty(book.ISBN))
                        {
                            books.Add(book);
                            booksRead++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading book row: {ex.Message}");
                    }
                }

                books = books.GroupBy(b => b.ISBN).Select(g => g.First()).ToList();
                Console.WriteLine($"Read {booksRead} books from CSV, {books.Count} unique after deduplication.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading CSV file: {ex.Message}");
            }

            return books;
        }

        private static async Task InsertBooksWithRawSqlAsync(AppDbContext context, List<Book> books)
        {
            const int batchSize = 1000;
            int totalInserted = 0;
            int totalSkipped = 0;

            for (int i = 0; i < books.Count; i += batchSize)
            {
                var batch = books.Skip(i).Take(batchSize).ToList();
                
                try
                {
                    var values = batch.Select(b =>
                        $"('{EscapeSql(b.ISBN)}', '{EscapeSql(b.BookTitle)}', '{EscapeSql(b.BookAuthor)}', {b.YearOfPublication}, '{EscapeSql(b.Publisher)}', {(b.ImageUrlS != null ? $"'{EscapeSql(b.ImageUrlS)}'" : "NULL")}, {(b.ImageUrlM != null ? $"'{EscapeSql(b.ImageUrlM)}'" : "NULL")}, {(b.ImageUrlL != null ? $"'{EscapeSql(b.ImageUrlL)}'" : "NULL")}, '{EscapeSql(b.Genre)}', '{EscapeSql(b.Language)}', {b.PageCount}, '{EscapeSql(b.Description)}', {b.Rating}, '{EscapeSql(b.AvailabilityStatus)}', '{EscapeSql(b.Edition)}', '{EscapeSql(b.Format)}')"
                    );
                    var sql = "INSERT INTO Books (ISBN, BookTitle, BookAuthor, YearOfPublication, Publisher, ImageUrlS, ImageUrlM, ImageUrlL, Genre, Language, PageCount, Description, Rating, AvailabilityStatus, Edition, Format) VALUES " + string.Join(",", values);

                    await context.Database.ExecuteSqlRawAsync(sql);
                    totalInserted += batch.Count;
                    Console.WriteLine($"Inserted batch of {batch.Count} books. Total: {totalInserted}/{books.Count}");
                }
                catch (Exception ex)
                {
                    // If batch insert fails due to duplicates, try inserting one by one
                    Console.WriteLine($"Batch insert failed, trying individual inserts for this batch. Error: {ex.Message}");
                    foreach (var book in batch)
                    {
                        try
                        {
                            var sql = $"INSERT INTO Books (ISBN, BookTitle, BookAuthor, YearOfPublication, Publisher, ImageUrlS, ImageUrlM, ImageUrlL, Genre, Language, PageCount, Description, Rating, AvailabilityStatus, Edition, Format) VALUES ('{EscapeSql(book.ISBN)}', '{EscapeSql(book.BookTitle)}', '{EscapeSql(book.BookAuthor)}', {book.YearOfPublication}, '{EscapeSql(book.Publisher)}', {(book.ImageUrlS != null ? $"'{EscapeSql(book.ImageUrlS)}'" : "NULL")}, {(book.ImageUrlM != null ? $"'{EscapeSql(book.ImageUrlM)}'" : "NULL")}, {(book.ImageUrlL != null ? $"'{EscapeSql(book.ImageUrlL)}'" : "NULL")}, '{EscapeSql(book.Genre)}', '{EscapeSql(book.Language)}', {book.PageCount}, '{EscapeSql(book.Description)}', {book.Rating}, '{EscapeSql(book.AvailabilityStatus)}', '{EscapeSql(book.Edition)}', '{EscapeSql(book.Format)}')";
                            await context.Database.ExecuteSqlRawAsync(sql);
                            totalInserted++;
                        }
                        catch
                        {
                            // Skip duplicate books silently
                            totalSkipped++;
                        }
                    }
                    Console.WriteLine($"Processed batch individually. Total inserted: {totalInserted}, Total skipped: {totalSkipped}");
                }
            }
            Console.WriteLine($"Final statistics - Inserted: {totalInserted}, Skipped (duplicates): {totalSkipped}");
        }

        private static string EscapeSql(string? value)
        {
            if (value == null) return string.Empty;
            return value.Replace("'", "''");
        }
    }
}