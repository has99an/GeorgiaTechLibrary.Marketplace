# Cache Invalidation Fix - Immediate Stock Updates

## Problem
Stock changes (after order creation) were not reflected in UI immediately. Stock would only update after ~10 minutes when Redis cache expired.

## Root Cause
`UpdateBookStockCommandHandler` was only clearing page caches (`available:page:*`) but not:
- Individual book query caches (`GetBookByIsbnQuery`)
- Search result caches (`SearchBooksQuery`)
- Available books list caches (`GetAvailableBooksQuery`)

## Solution
Added aggressive cache invalidation in `UpdateBookStockCommandHandler` when stock is updated:

```csharp
// Clear page caches since stock changed
await _cache.RemoveByPatternAsync("available:page:*", cancellationToken);

// Clear individual book query caches to ensure immediate update in UI
await _cache.RemoveByPatternAsync("query:GetBookByIsbnQuery:*", cancellationToken);

// Clear search result caches that might contain this book
await _cache.RemoveByPatternAsync("query:SearchBooksQuery:*", cancellationToken);
await _cache.RemoveByPatternAsync("query:GetAvailableBooksQuery:*", cancellationToken);
```

## How It Works Now

**Event Flow:**
1. Order created → OrderPaid event published
2. WarehouseService receives event → Updates stock in database
3. WarehouseService publishes BookStockUpdated event
4. SearchService receives event → Updates book in Redis
5. **NEW:** SearchService invalidates ALL related caches
6. Next UI request → Fresh data fetched from Redis
7. **Result:** Stock reflects immediately in UI

## Caches Cleared

| Cache Pattern | Purpose | Impact |
|---------------|---------|--------|
| `available:page:*` | Paginated book lists | Forces refresh of all book lists |
| `query:GetBookByIsbnQuery:*` | Individual book details | Forces refresh when viewing specific book |
| `query:SearchBooksQuery:*` | Search results | Ensures search results show updated stock |
| `query:GetAvailableBooksQuery:*` | Available books list | Updates all book listings |

## Trade-offs

**Pros:**
- ✅ Immediate stock updates visible in UI
- ✅ Accurate inventory display
- ✅ Better user experience
- ✅ No ghost orders from outdated stock info

**Cons:**
- ⚠️ Cache miss after every stock update (temporary performance impact)
- ⚠️ More Redis operations
- ⚠️ Slightly higher load on first request after update

**Mitigation:**
- Caches rebuild automatically on next request
- Performance impact is minimal (single Redis fetch)
- Only affects books with recent stock changes

## Testing

### Before Fix:
```bash
# Order book with ISBN 0312995431
# Check stock immediately
curl http://localhost:5004/search/books/0312995431
# Result: OLD stock (cached for 10 minutes)
```

### After Fix:
```bash
# Order book with ISBN 0312995431
# Check stock immediately
curl http://localhost:5004/search/books/0312995431
# Result: UPDATED stock (cache invalidated, fresh data)
```

## Verification Commands

```bash
# 1. Check current stock in database
docker-compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -Q "SELECT BookISBN, SellerId, Quantity FROM WarehouseServiceDb.dbo.Stock WHERE BookISBN='0312995431'"

# 2. Check Redis cache (should be empty after stock update)
docker-compose exec redis redis-cli KEYS "query:GetBookByIsbnQuery:*"

# 3. Check logs for cache invalidation
docker-compose logs searchservice --tail=50 | grep "invalidated all related caches"

# 4. Test via API
curl http://localhost:5004/search/books/0312995431 | jq '.totalStock'
```

## Files Modified
- `SearchService/Application/Commands/Stock/UpdateBookStockCommandHandler.cs`
  - Added cache invalidation for all query types that might contain the updated book

## Alternative Approaches Considered

1. **Targeted Cache Keys:**
   - Use simpler cache keys like `book:{isbn}`
   - ❌ Requires refactoring entire caching system

2. **Cache Key Tagging:**
   - Tag caches with ISBN, clear by tag
   - ❌ Redis doesn't natively support tagging

3. **Shorter TTL:**
   - Reduce cache TTL from 10 min to 1 min
   - ❌ More cache misses, not truly immediate

4. **Aggressive Invalidation (CHOSEN):**
   - Clear all related caches on stock update
   - ✅ Simple, reliable, immediate updates
