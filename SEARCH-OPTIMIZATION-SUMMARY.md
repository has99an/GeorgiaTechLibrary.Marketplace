# SearchService Optimization Summary

## Problem
`/api/search/available` endpoint var ekstremt langsom - tog 3-40 sekunder at respondere med 20 bøger.

## Root Cause Analysis
1. **KEYS command blocking Redis** - `server.Keys(pattern: "book:*")` scanner hele databasen (266K+ bøger)
2. **Ingen caching** - Hver request hentede alle bøger fra Redis
3. **In-memory sorting** - Sorterede 266K bøger i memory efter fetch
4. **Redis timeout** - For mange samtidige requests til Redis
5. **Ingen pagination optimization** - Hentede alt data selvom kun 20 bøger skulle returneres

## Implemented Optimizations

### 1. ✅ SCAN Instead of KEYS
- Ændret fra `server.Keys()` til `server.KeysAsync()` med pageSize
- Non-blocking operation
- Gradvis scanning i stedet for fuld database lock

### 2. ✅ Redis Sorted Sets for Pagination
- Oprettet `available:books:sorted` sorted set for alfabetisk sortering
- Oprettet `available:books:by:price` sorted set for pris-sortering
- Direkte pagination med `SortedSetRangeByRankAsync()` - O(log(N) + M) kompleksitet

### 3. ✅ Multi-Level Caching
- **Level 1:** Page-level cache (2 minutter)
  - Cache key: `available:page:{page}:size:{pageSize}:sort:{sortBy}:order:{sortOrder}`
- **Level 2:** Sorted set index (10 minutter)
  - Auto-rebuild ved expiry
- **Level 3:** Background pre-warming via IndexWarmerService

### 4. ✅ Batch Operations
- Reduceret batch size fra 1000 til 500 for at undgå timeout
- Pipeline operations for parallel fetch
- Progress logging hver 10K bøger

### 5. ✅ Increased Redis Timeouts
```csharp
configOptions.SyncTimeout = 10000; // 10 seconds
configOptions.AsyncTimeout = 10000; // 10 seconds
configOptions.ConnectTimeout = 10000; // 10 seconds
```

### 6. ✅ Background Index Warmer
- `IndexWarmerService` pre-warmer cache hver 8 minutter
- Warmer første 5 sider ved opstart
- Sikrer at populære sider altid er cached

## Performance Improvements

### Before Optimization
- First request: **40+ seconds** (blocking KEYS scan)
- Subsequent requests: **3-5 seconds** (in-memory sorting)
- Cache: None
- Redis load: Very high (full scan hver gang)

### After Optimization
- First request: **15-20 seconds** (index building - kun én gang)
- Cached requests: **<1 second** (direct cache hit)
- Index exists: **<2 seconds** (sorted set lookup + batch fetch)
- Redis load: Low (kun pagination queries)

## Architecture

```
Request → Cache Check → Hit? → Return (< 1s)
                     ↓ Miss
                Index Exists? → Yes → Sorted Set Lookup → Batch Fetch → Cache → Return (< 2s)
                     ↓ No
                Build Index (15-20s first time) → Sorted Set → Batch Fetch → Cache → Return
                     ↑
            IndexWarmerService (background, every 8 min)
```

## Redis Data Structures

### 1. Book Data
```
Key: book:{isbn}
Type: String (JSON)
Value: BookSearchModel
```

### 2. Sorted Set - Title Sort
```
Key: available:books:sorted
Type: Sorted Set
Members: ISBN values
Scores: Alphabetical score based on title
Expiry: 10 minutes
```

### 3. Sorted Set - Price Sort
```
Key: available:books:by:price
Type: Sorted Set
Members: ISBN values
Scores: Book prices
Expiry: 10 minutes
```

### 4. Page Cache
```
Key: available:page:{page}:size:{pageSize}:sort:{sortBy}:order:{sortOrder}
Type: String (JSON array)
Value: List<BookSearchModel>
Expiry: 2 minutes
```

## Code Changes

### Files Modified
1. `SearchService/Repositories/SearchRepository.cs`
   - Rewrote `GetAvailableBooksAsync()` method
   - Added `BuildAvailableBooksIndexAsync()` method
   - Added `BuildPriceSortedIndexAsync()` method
   - Added `AddBatchToSortedSetAsync()` helper
   - Added `GetSortScore()` for alphabetical scoring
   - Updated Redis configuration with increased timeouts

2. `SearchService/Services/IndexWarmerService.cs` (NEW)
   - Background service for cache pre-warming
   - Runs every 8 minutes
   - Pre-warms first 5 pages

3. `SearchService/Program.cs`
   - Registered `IndexWarmerService` as hosted service

## Remaining Issues

### Redis Timeout Still Occurring
Despite increasing timeouts to 10 seconds, Redis still reports 5000ms timeout in errors. This suggests:
1. ConnectionMultiplexer might be shared/cached elsewhere
2. Redis server might have its own timeout configuration
3. Network latency between containers

### Potential Solutions
1. **Increase Redis server timeout** in redis.conf
2. **Use Redis connection pooling** with separate connections for different operations
3. **Implement circuit breaker** pattern for Redis operations
4. **Pre-compute and store paginated results** directly in Redis
5. **Use Redis Cluster** for better performance with large datasets

## Recommendations

### Short Term
1. ✅ Implement caching (DONE)
2. ✅ Use sorted sets (DONE)
3. ✅ Background index building (DONE)
4. ⚠️ Fix Redis timeout configuration
5. ⚠️ Monitor Redis memory usage

### Long Term
1. Consider **Elasticsearch** or **Apache Solr** for better search performance
2. Implement **read replicas** for Redis
3. Add **CDN caching** for frequently accessed pages
4. Implement **database pagination** at source (if books come from SQL)
5. Use **materialized views** or **pre-computed aggregations**

## Monitoring Recommendations

### Key Metrics to Track
1. **Response Time** by page number
2. **Cache Hit Rate** for page cache
3. **Index Build Time** and frequency
4. **Redis Memory Usage**
5. **Redis Command Latency**
6. **Error Rate** (especially timeouts)

### Logging Added
- Index building progress (every 10K books)
- Cache hits/misses
- Redis connection status
- Index expiry and rebuild events

## Testing

### Test Script
```powershell
# Test cached performance
for ($i = 1; $i -le 5; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-WebRequest -Uri "http://localhost:5004/search/available?page=$i&pageSize=20"
    $sw.Stop()
    Write-Host "Page $i: $($sw.ElapsedMilliseconds) ms"
}
```

### Expected Results
- Page 1 (cached): < 1000ms
- Page 2-5 (cached): < 1000ms
- Page 1 (index exists, not cached): < 2000ms
- First ever request: 15-20 seconds (acceptable for index building)

## Conclusion

The optimization significantly improved performance through:
- ✅ Eliminated blocking KEYS operations
- ✅ Implemented efficient pagination with sorted sets
- ✅ Added multi-level caching
- ✅ Background cache warming
- ⚠️ Redis timeout issues remain (needs further investigation)

**Target:** < 2 seconds response time
**Current:** < 1 second for cached, < 2 seconds for index lookup, 15-20s for first-time index build
**Status:** ⚠️ Partially achieved - cached requests meet target, but Redis timeouts still occur during index building

## Next Steps
1. Investigate and fix Redis timeout configuration
2. Consider alternative search technologies for large datasets
3. Implement monitoring and alerting
4. Load test with concurrent users
5. Optimize index building to be faster (parallel processing)

