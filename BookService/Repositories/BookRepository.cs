using BookService.Data;
using BookService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookService.Repositories;

public class BookRepository : IBookRepository
{
    private readonly AppDbContext _context;

    public BookRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Book>> GetAllBooksAsync()
    {
        return await _context.Books.ToListAsync();
    }

    public async Task<Book?> GetBookByIsbnAsync(string isbn)
    {
        return await _context.Books.FindAsync(isbn);
    }

    public async Task<Book> AddBookAsync(Book book)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return book;
    }

    public async Task<Book?> UpdateBookAsync(string isbn, Book book)
    {
        var existingBook = await _context.Books.FindAsync(isbn);
        if (existingBook == null)
        {
            return null;
        }

        existingBook.BookTitle = book.BookTitle;
        existingBook.BookAuthor = book.BookAuthor;
        existingBook.YearOfPublication = book.YearOfPublication;
        existingBook.Publisher = book.Publisher;
        existingBook.ImageUrlS = book.ImageUrlS;
        existingBook.ImageUrlM = book.ImageUrlM;
        existingBook.ImageUrlL = book.ImageUrlL;

        await _context.SaveChangesAsync();
        return existingBook;
    }

    public async Task<bool> DeleteBookAsync(string isbn)
    {
        var book = await _context.Books.FindAsync(isbn);
        if (book == null)
        {
            return false;
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BookExistsAsync(string isbn)
    {
        return await _context.Books.AnyAsync(b => b.ISBN == isbn);
    }
}
