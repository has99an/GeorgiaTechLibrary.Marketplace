# SearchService

## Description

The SearchService provides fast, scalable book search functionality for the Georgia Tech Library Marketplace. It maintains an in-memory search index using Redis and keeps it synchronized through event-driven updates from other services. The service handles:

- **Full-Text Search**: Search books by title, author, or ISBN
- **Real-Time Indexing**: Automatically updates search index when books are added/modified
- **Inventory Aggregation**: Combines book data with stock information from multiple sellers
- **High Performance**: Uses Redis for sub-millisecond search responses

The SearchService fits into the overall architecture as the search and discovery engine, enabling users to find books efficiently across the distributed marketplace.

## API Endpoints

### Search Books
- `GET /api/search?query={search-term}` - Search for books

**Query Parameters:**
- `query` (required): Search term for title, author, or ISBN

**Response (200 OK):**
```json
[
  {
    "isbn": "1234567890123",
    "title": "Sample Book Title",
    "author": "Author Name",
    "totalStock": 15,
    "availableSellers": 3,
    "minPrice": 19.99
  }
]
```

### Health Check
- `GET /api/search/health` - Service health status

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2025-11-11T04:00:00Z"
}
```

### System Health Check
- `GET /health` - Overall service health including Redis connectivity

## Database Model

The SearchService does not use a traditional relational database. Instead, it maintains a search index in Redis with the following structure:

**Redis Data Structure:**
```
BookSearch:{ISBN} → Hash
├── isbn: String
├── title: String
├── author: String
├── totalStock: Integer
├── availableSellers: Integer
├── minPrice: Decimal

SearchIndex:title → Sorted Set (for title-based search)
SearchIndex:author → Sorted Set (for author-based search)
SearchIndex:isbn → Hash (for ISBN lookup)
```

**Search Index Schema:**
```
BookSearchModel
├── Isbn: String (Primary Key)
├── Title: String (Indexed for search)
├── Author: String (Indexed for search)
├── TotalStock: Integer (Aggregated from all sellers)
├── AvailableSellers: Integer (Count of sellers with stock)
└── MinPrice: Decimal (Lowest price across sellers)
```

## Events

### Consumed Events

All events are consumed from the `book_events` exchange.

**BookCreated** (Routing Key: `BookCreated`)
```json
{
  "ISBN": "1234567890123",
  "BookTitle": "New Book Title",
  "BookAuthor": "Author Name",
  "YearOfPublication": 2023,
  "Publisher": "Publisher Inc",
  "ImageUrlS": "http://example.com/small.jpg",
  "ImageUrlM": "http://example.com/medium.jpg",
  "ImageUrlL": "http://example.com/large.jpg"
}
```
*Consumed when:* A new book is added to the catalog
*Action:* Creates new search index entry

**BookUpdated** (Routing Key: `BookUpdated`)
- *Same structure as BookCreated*
- *Consumed when:* Book information is modified
- *Action:* Updates existing search index entry

**BookDeleted** (Routing Key: `BookDeleted`)
- *Same structure as BookCreated*
- *Consumed when:* A book is removed from the catalog
- *Action:* Removes book from search index

**BookStockUpdated** (Routing Key: `BookStockUpdated`)
```json
{
  "id": 1,
  "bookISBN": "1234567890123",
  "sellerId": "seller456",
  "quantity": 5,
  "price": 22.99,
  "condition": "New"
}
```
*Consumed when:* Warehouse inventory changes
*Action:* Updates stock aggregation (totalStock, availableSellers, minPrice)

### Event Flow

1. BookService creates/updates/deletes book → Publishes book events
2. SearchService consumes book events → Updates search index
3. WarehouseService updates stock → Publishes stock events
4. SearchService consumes stock events → Updates inventory aggregations
5. User searches → SearchService queries Redis index → Returns results

## Dependencies

- **Redis**: For storing and querying the search index
- **RabbitMQ**: For consuming book and stock update events
- **BookService**: Publishes book catalog events
- **WarehouseService**: Publishes inventory update events

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build searchservice

# Run the service
docker-compose up searchservice
```

The service will be available at `http://localhost:5002`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `RabbitMQ__HostName`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)
- `Redis__ConnectionString`: Redis connection string (default: `localhost:6379`)

## Testing

### Using .http File

The service includes `SearchService.http` for testing endpoints:

```http
### Search Books
GET http://localhost:5002/api/search?query=harry

### Health Check
GET http://localhost:5002/api/search/health
```

### Manual Testing with curl

```bash
# Search for books
curl "http://localhost:5002/api/search?query=potter"

# Health check
curl http://localhost:5002/api/search/health

# System health check
curl http://localhost:5002/health
```

### Event Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify event consumption.

### Redis Testing

Connect to Redis to inspect the search index:

```bash
# Connect to Redis
redis-cli

# View search index entries
KEYS BookSearch:*

# Get a specific book
HGETALL BookSearch:1234567890123

# Check search sets
ZRANGE SearchIndex:title 0 -1 WITHSCORES
```

### Integration Testing

1. **Add Book via BookService**: Verify `BookCreated` event is consumed and index is updated
2. **Update Stock via WarehouseService**: Verify `BookStockUpdated` event updates aggregations
3. **Search**: Verify search returns correct results with updated stock information
4. **Delete Book**: Verify book is removed from search results

### Performance Testing

The service is designed for high-performance search. Test with large datasets to verify Redis performance:

```bash
# Load test with Apache Bench
ab -n 1000 -c 10 "http://localhost:5002/api/search?query=test"
```

### Expected Behavior

- **Empty Query**: Returns 400 Bad Request
- **No Results**: Returns empty array `[]`
- **Partial Matches**: Returns books with matching title/author
- **Stock Updates**: Real-time reflection in search results
- **Redis Down**: Health check fails, service continues with cached data
