using SearchService.Models;

namespace SearchService.Repositories;

public interface ISearchRepository
{
    Task<BookSearchModel?> GetBookByIsbnAsync(string isbn);
    Task<IEnumerable<BookSearchModel>> SearchBooksAsync(string query);
    Task AddOrUpdateBookAsync(BookSearchModel book);
    Task DeleteBookAsync(string isbn);
    Task UpdateBookStockAsync(string isbn, int totalStock, int availableSellers, decimal minPrice);
}
