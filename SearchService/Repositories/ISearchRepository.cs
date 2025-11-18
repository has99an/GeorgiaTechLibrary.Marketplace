using SearchService.Models;

namespace SearchService.Repositories;

public interface ISearchRepository
{
    Task<BookSearchModel?> GetBookByIsbnAsync(string isbn);
    Task<IEnumerable<BookSearchModel>> SearchBooksAsync(string query);
    Task AddOrUpdateBookAsync(BookSearchModel book);
    Task DeleteBookAsync(string isbn);
    Task UpdateBookStockAsync(string isbn, int totalStock, int availableSellers, decimal minPrice);

     Task<PagedResult<BookSearchModel>> GetAvailableBooksAsync(int page = 1, int pageSize = 20, string? sortBy = null, string? sortOrder = "asc");
    Task<IEnumerable<BookSearchModel>> GetFeaturedBooksAsync(int count = 8);
    Task<IEnumerable<SellerInfo>> GetBookSellersAsync(string isbn);
    Task<SearchStats> GetSearchStatsAsync();
    Task UpdateBookWarehouseDataAsync(string isbn, List<WarehouseItem> warehouseItems);
}
