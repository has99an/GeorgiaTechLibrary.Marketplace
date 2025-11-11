# BookService

## Description

The BookService manages the book catalog for the Georgia Tech Library Marketplace. It provides comprehensive CRUD operations for book management and serves as the central source of truth for book data. The service handles:

- **Book Catalog Management**: Create, read, update, and delete book records
- **Data Synchronization**: Publishes events when books are modified to keep other services in sync
- **Bulk Operations**: Supports syncing existing book data via events for system initialization

The BookService fits into the overall architecture as the book data authority, providing book information to services like SearchService, OrderService, and WarehouseService through event-driven updates.

## API Endpoints

### Get All Books
- `GET /api/books` - Retrieve all books in the catalog

**Response (200 OK):**
```json
[
  {
    "isbn": "1234567890123",
    "bookTitle": "Sample Book",
    "bookAuthor": "Author Name",
    "yearOfPublication": 2023,
    "publisher": "Publisher Inc",
    "imageUrlS": "http://example.com/small.jpg",
    "imageUrlM": "http://example.com/medium.jpg",
    "imageUrlL": "http://example.com/large.jpg"
  }
]
```

### Get Book by ISBN
- `GET /api/books/{isbn}` - Retrieve a specific book by ISBN

**Response (200 OK):** Single book object as above

### Create Book
- `POST /api/books` - Add a new book to the catalog

**Request Body:**
```json
{
  "isbn": "1234567890123",
  "bookTitle": "New Book Title",
  "bookAuthor": "Author Name",
  "yearOfPublication": 2023,
  "publisher": "Publisher Inc",
  "imageUrlS": "http://example.com/small.jpg",
  "imageUrlM": "http://example.com/medium.jpg",
  "imageUrlL": "http://example.com/large.jpg"
}
```

**Response (201 Created):** Created book object

### Update Book
- `PUT /api/books/{isbn}` - Update an existing book

**Request Body:** (all fields optional)
```json
{
  "bookTitle": "Updated Title",
  "yearOfPublication": 2024
}
```

**Response (200 OK):** Updated book object

### Delete Book
- `DELETE /api/books/{isbn}` - Remove a book from the catalog

**Response (204 No Content):** Success

### Sync Events
- `POST /api/books/sync-events` - Publish BookCreated events for all existing books

**Response (200 OK):**
```json
42
```
*Returns the number of books synced*

### Health Check
- `GET /health` - Service health status

## Database Model

### Books Table

| Column | Type | Description |
|--------|------|-------------|
| ISBN | NVARCHAR(13) (PK) | Book ISBN (unique identifier) |
| BookTitle | NVARCHAR(500) | Book title |
| BookAuthor | NVARCHAR(200) | Author name |
| YearOfPublication | INT | Publication year |
| Publisher | NVARCHAR(200) | Publisher name |
| ImageUrlS | NVARCHAR(500) | Small image URL |
| ImageUrlM | NVARCHAR(500) | Medium image URL |
| ImageUrlL | NVARCHAR(500) | Large image URL |

**Entity Diagram:**
```
Books
├── ISBN: String (Primary Key, Max 13 chars)
├── BookTitle: String (Required, Max 500 chars)
├── BookAuthor: String (Required, Max 200 chars)
├── YearOfPublication: Int (Required)
├── Publisher: String (Required, Max 200 chars)
├── ImageUrlS: String (Optional, Max 500 chars)
├── ImageUrlM: String (Optional, Max 500 chars)
└── ImageUrlL: String (Optional, Max 500 chars)
```

## Events

### Published Events

All events are published to the `book_events` exchange.

**BookCreated** (Routing Key: `BookCreated`)
```json
{
  "isbn": "1234567890123",
  "bookTitle": "Sample Book",
  "bookAuthor": "Author Name",
  "yearOfPublication": 2023,
  "publisher": "Publisher Inc",
  "imageUrlS": "http://example.com/small.jpg",
  "imageUrlM": "http://example.com/medium.jpg",
  "imageUrlL": "http://example.com/large.jpg"
}
```
*Published when:* A new book is created
*Consumed by:* SearchService for indexing

**BookUpdated** (Routing Key: `BookUpdated`)
- *Same structure as BookCreated*
- *Published when:* An existing book is modified
- *Consumed by:* SearchService for re-indexing

**BookDeleted** (Routing Key: `BookDeleted`)
- *Same structure as BookCreated*
- *Published when:* A book is deleted
- *Consumed by:* SearchService for removal from index

### Event Flow

1. Book created/updated/deleted in BookService → Publishes event
2. SearchService consumes event → Updates search index
3. WarehouseService may consume events → Updates inventory metadata
4. OrderService may consume events → Updates order item details

## Dependencies

- **SQL Server**: For storing book catalog data (Books table)
- **RabbitMQ**: For publishing book change events
- **SearchService**: Consumes book events for search indexing
- **WarehouseService**: May consume book events for inventory management
- **OrderService**: May consume book events for order processing

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build bookservice

# Run the service
docker-compose up bookservice
```

The service will be available at `http://localhost:5000`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)

### Database Migration

The service automatically runs EF Core migrations on startup and seeds initial data from `Data/Books.csv`.

## Testing

### Using .http File

The service includes `BookService.http` for testing endpoints:

```http
### Get All Books
GET http://localhost:5000/api/books

### Get Book by ISBN
GET http://localhost:5000/api/books/1234567890123

### Create Book
POST http://localhost:5000/api/books
Content-Type: application/json

{
  "isbn": "1234567890123",
  "bookTitle": "Test Book",
  "bookAuthor": "Test Author",
  "yearOfPublication": 2023,
  "publisher": "Test Publisher"
}

### Update Book
PUT http://localhost:5000/api/books/1234567890123
Content-Type: application/json

{
  "bookTitle": "Updated Test Book"
}

### Delete Book
DELETE http://localhost:5000/api/books/1234567890123

### Sync Events
POST http://localhost:5000/api/books/sync-events
```

### Manual Testing with curl

```bash
# Get all books
curl http://localhost:5000/api/books

# Create a book
curl -X POST http://localhost:5000/api/books \
  -H "Content-Type: application/json" \
  -d '{
    "isbn": "1234567890123",
    "bookTitle": "Test Book",
    "bookAuthor": "Test Author",
    "yearOfPublication": 2023,
    "publisher": "Test Publisher"
  }'

# Health check
curl http://localhost:5000/health
```

### Event Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify event publishing when books are created/updated/deleted.

### Database Testing

Connect to SQL Server to verify Books table:

```sql
SELECT TOP 10 * FROM Books;
