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
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "books.csv");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"CSV file not found at: {csvPath}");
                return books;
            }

            try
            {
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Skip header row
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
                            ImageUrlL = csv.GetField("Image-URL-L")
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

                // Deduplicate by ISBN
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

            for (int i = 0; i < books.Count; i += batchSize)
            {
                var batch = books.Skip(i).Take(batchSize).ToList();
                var values = batch.Select(b =>
                    $"('{EscapeSql(b.ISBN)}', '{EscapeSql(b.BookTitle)}', '{EscapeSql(b.BookAuthor)}', {b.YearOfPublication}, '{EscapeSql(b.Publisher)}', {(b.ImageUrlS != null ? $"'{EscapeSql(b.ImageUrlS)}'" : "NULL")}, {(b.ImageUrlM != null ? $"'{EscapeSql(b.ImageUrlM)}'" : "NULL")}, {(b.ImageUrlL != null ? $"'{EscapeSql(b.ImageUrlL)}'" : "NULL")})"
                );
                var sql = "INSERT INTO Books (ISBN, BookTitle, BookAuthor, YearOfPublication, Publisher, ImageUrlS, ImageUrlM, ImageUrlL) VALUES " + string.Join(",", values);

                await context.Database.ExecuteSqlRawAsync(sql);
                totalInserted += batch.Count;
                Console.WriteLine($"Inserted batch of {batch.Count} books. Total: {totalInserted}/{books.Count}");
            }
        }

        private static string EscapeSql(string? value)
        {
            if (value == null) return string.Empty;
            return value.Replace("'", "''");
        }
    }
}
