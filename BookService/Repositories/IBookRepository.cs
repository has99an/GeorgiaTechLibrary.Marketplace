using BookService.Models;

namespace BookService.Repositories;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetAllBooksAsync();
    Task<Book?> GetBookByIsbnAsync(string isbn);
    Task<Book> AddBookAsync(Book book);
    Task<Book?> UpdateBookAsync(string isbn, Book book);
    Task<bool> DeleteBookAsync(string isbn);
    Task<bool> BookExistsAsync(string isbn);
}
