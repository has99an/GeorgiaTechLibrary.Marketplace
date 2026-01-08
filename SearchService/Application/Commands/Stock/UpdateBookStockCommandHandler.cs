using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Domain.ValueObjects;
using System.Text.Json;

namespace SearchService.Application.Commands.Stock;

/// <summary>
/// Handler for UpdateBookStockCommand
/// </summary>
public class UpdateBookStockCommandHandler : IRequestHandler<UpdateBookStockCommand, UpdateBookStockResult>
{
    private readonly IBookRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<UpdateBookStockCommandHandler> _logger;

    public UpdateBookStockCommandHandler(
        IBookRepository repository,
        ICacheService cache,
        ILogger<UpdateBookStockCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UpdateBookStockResult> Handle(UpdateBookStockCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating stock for book ISBN: {ISBN}", request.BookISBN);

            var isbn = ISBN.Create(request.BookISBN);
            var book = await _repository.GetByIsbnAsync(isbn, cancellationToken);

            if (book == null)
            {
                _logger.LogWarning("Book not found with ISBN: {ISBN}", request.BookISBN);
                return new UpdateBookStockResult(false, "Book not found");
            }

            book.UpdateStock(request.TotalStock, request.AvailableSellers, request.MinPrice);
            
            await _repository.AddOrUpdateAsync(book, cancellationToken);
            
            // Update sellers data in Redis
            await UpdateSellersDataAsync(request.BookISBN, request.Sellers, cancellationToken);
            
            // 🔥 NUCLEAR CACHE INVALIDATION 🔥
            // Clear EVERY possible cache that could show this book
            // This ensures stock updates are IMMEDIATELY visible in ALL UI views
            
            _logger.LogInformation("🔥 Starting NUCLEAR cache invalidation for ISBN: {ISBN}", request.BookISBN);
            
            // 1. Clear ALL query result caches (MediatR CachingBehavior)
            await _cache.RemoveByPatternAsync("query:*", cancellationToken);
            _logger.LogInformation("✓ Nuked ALL query:* caches (covers all endpoints)");
            
            // 2. Clear ALL page caches
            await _cache.RemoveByPatternAsync("available:page:*", cancellationToken);
            await _cache.RemoveByPatternAsync("page:*", cancellationToken);
            _logger.LogInformation("✓ Nuked ALL page:* caches");
            
            // 3. Clear individual book cache
            await _cache.RemoveAsync($"book:{request.BookISBN}", cancellationToken);
            _logger.LogInformation("✓ Cleared book:{ISBN} cache", request.BookISBN);
            
            // 4. Clear search index caches
            await _cache.RemoveByPatternAsync("index:*", cancellationToken);
            _logger.LogInformation("✓ Nuked ALL index:* caches");
            
            // 5. Clear facet and filter caches
            await _cache.RemoveByPatternAsync("facet:*", cancellationToken);
            await _cache.RemoveByPatternAsync("filter:*", cancellationToken);
            _logger.LogInformation("✓ Cleared facet and filter caches");
            
            // 6. Clear stats and analytics
            await _cache.RemoveByPatternAsync("stats:*", cancellationToken);
            await _cache.RemoveByPatternAsync("analytics:*", cancellationToken);
            _logger.LogInformation("✓ Cleared stats and analytics caches");

            _logger.LogInformation("✅ NUCLEAR CACHE INVALIDATION COMPLETE! Stock for ISBN {ISBN} should be visible INSTANTLY across ALL views", request.BookISBN);

            return new UpdateBookStockResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for book ISBN: {ISBN}", request.BookISBN);
            return new UpdateBookStockResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Updates sellers data in Redis cache
    /// </summary>
    private async Task UpdateSellersDataAsync(string bookISBN, List<Application.Common.Models.SellerInfoDto>? sellers, CancellationToken cancellationToken)
    {
        try
        {
            var sellersKey = $"sellers:{bookISBN}";

            if (sellers != null && sellers.Any())
            {
                // Filter out sellers with invalid data AND quantity = 0 (only include sellers with stock > 0)
                var validSellers = sellers
                    .Where(s => !string.IsNullOrWhiteSpace(s.SellerId) && s.Quantity > 0 && s.Price >= 0)
                    .ToList();

                if (!validSellers.Any())
                {
                    _logger.LogWarning("All sellers for ISBN {ISBN} have invalid data or zero quantity. Removing sellers key.", bookISBN);
                    await _cache.RemoveAsync(sellersKey, cancellationToken);
                    return;
                }

                // Remove duplicates based on SellerId (in case of duplicate events)
                var uniqueSellers = validSellers
                    .GroupBy(s => s.SellerId)
                    .Select(g => g.OrderByDescending(s => s.LastUpdated).First()) // Take most recent if duplicates
                    .ToList();

                var sellersJson = JsonSerializer.Serialize(uniqueSellers);
                await _cache.SetAsync(sellersKey, sellersJson, cancellationToken: cancellationToken);
                
                _logger.LogInformation("Updated sellers data for ISBN: {ISBN} with {Count} unique valid sellers with stock > 0 (filtered from {OriginalCount})", 
                    bookISBN, uniqueSellers.Count, sellers.Count);
            }
            else
            {
                // If no sellers, remove the key to avoid stale data
                await _cache.RemoveAsync(sellersKey, cancellationToken);
                _logger.LogInformation("Removed sellers data for ISBN: {ISBN} (no sellers available or backward compatibility)", 
                    bookISBN);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - allow book update to succeed even if sellers update fails
            _logger.LogError(ex, "Error updating sellers data for ISBN: {ISBN}. Book data was updated successfully.", bookISBN);
        }
    }
}

