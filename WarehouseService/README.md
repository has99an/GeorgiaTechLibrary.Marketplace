**Kort svar:** Ja, dokumentationen skal opdateres! Her er den opdaterede version:

```markdown
# WarehouseService

## Description

The WarehouseService manages inventory and stock levels for books in the Georgia Tech Library Marketplace. It tracks which sellers have which books in stock, at what prices, and whether they are new or used. The service provides comprehensive inventory management and ensures real-time stock updates across the system. The service handles:

- **Inventory Management**: Track book stock by seller with pricing and condition
- **Stock Updates**: Adjust inventory levels and publish changes
- **Multi-Seller Support**: Support multiple sellers offering the same book
- **Event-Driven Updates**: Publish stock changes to keep search and order services in sync
- **Seller Types**: Distinguish between GT-Library (new books) and student sellers (used books)

The WarehouseService fits into the overall architecture as the inventory authority, providing stock information to SearchService for accurate availability and pricing, and to OrderService for stock validation.

## API Endpoints

### Get All Warehouse Items
- `GET /api/warehouse/items` - Retrieve all inventory items

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "bookISBN": "1234567890123",
    "sellerId": "student-c1235296",
    "quantity": 5,
    "price": 22.99,
    "location": "Campus-2",
    "isNew": false
  }
]
```

### Get Items by Book ISBN
- `GET /api/warehouse/items/{bookIsbn}` - Get all sellers' inventory for a specific book

**Response (200 OK):** Array of warehouse items for the book

### Get Items by Seller
- `GET /api/warehouse/sellers/{sellerId}/items` - Get all inventory items for a specific seller

**Response (200 OK):** Array of warehouse items for the seller

### Get New Books Only
- `GET /api/warehouse/items/new` - Get only new books (from GT-Library)

**Response (200 OK):** Array of new warehouse items

### Get Used Books Only
- `GET /api/warehouse/items/used` - Get only used books (from student sellers)

**Response (200 OK):** Array of used warehouse items

### Get Item by ID
- `GET /api/warehouse/items/{id}` - Retrieve a specific inventory item

**Response (200 OK):** Single warehouse item object

### Create Warehouse Item
- `POST /api/warehouse/items` - Add a new inventory item

**Request Body:**
```json
{
  "bookISBN": "1234567890123",
  "sellerId": "student-c1235296",
  "quantity": 10,
  "price": 19.99,
  "location": "Campus-2",
  "isNew": false
}
```

**Response (201 Created):** Created warehouse item

### Update Warehouse Item
- `PUT /api/warehouse/items/{id}` - Update an existing inventory item

**Request Body:** (all fields optional)
```json
{
  "quantity": 15,
  "price": 18.99,
  "location": "Campus-3",
  "isNew": false
}
```

**Response (200 OK):** Updated warehouse item

### Adjust Stock
- `POST /api/warehouse/adjust-stock` - Adjust stock quantity for a book-seller combination

**Request Body:**
```json
{
  "bookISBN": "1234567890123",
  "sellerId": "student-c1235296",
  "quantityChange": -2
}
```

**Response (200 OK):**
```json
{
  "message": "Stock adjusted successfully",
  "newQuantity": 8
}
```

### Delete Warehouse Item
- `DELETE /api/warehouse/items/{id}` - Remove an inventory item

**Response (204 No Content):** Item deleted successfully

### Health Check
- `GET /health` - Service health status

## Database Model

### WarehouseItems Table

| Column | Type | Description |
|--------|------|-------------|
| Id | INT (PK, Identity) | Unique inventory item identifier |
| BookISBN | NVARCHAR(13) | ISBN of the book |
| SellerId | NVARCHAR(100) | ID of the seller |
| Quantity | INT | Number of copies in stock |
| Price | DECIMAL(18,2) | Selling price per unit |
| Location | NVARCHAR(50) | Storage location ("Main Warehouse" or "Campus-X") |
| IsNew | BIT | Whether book is new (true) or used (false) |

**Entity Diagram:**
```
WarehouseItems
├── Id: Integer (Primary Key, Auto-increment)
├── BookISBN: String (Required, Max 13 chars)
├── SellerId: String (Required, Max 100 chars)
├── Quantity: Integer (Required, Min 0)
├── Price: Decimal (Required, Min 0.01)
├── Location: String (Required, Max 50 chars, Default: "Main Warehouse")
└── IsNew: Boolean (Required)
```

**Unique Constraint:** BookISBN + SellerId (one inventory record per book per seller)

## Events

### Published Events

All events are published to the `book_events` exchange.

**BookStockUpdated** (Routing Key: `BookStockUpdated`)
```json
{
  "id": 1,
  "bookISBN": "1234567890123",
  "sellerId": "student-c1235296",
  "quantity": 5,
  "price": 22.99,
  "location": "Campus-2",
  "isNew": false
}
```
*Published when:* Inventory is created, updated, or stock is adjusted
*Consumed by:* SearchService for inventory aggregation, OrderService for stock validation

**BookStockRemoved** (Routing Key: `BookStockRemoved`)
```json
{
  "id": 1,
  "bookISBN": "1234567890123",
  "sellerId": "student-c1235296"
}
```
*Published when:* Inventory item is deleted
*Consumed by:* SearchService to remove from search index

### Event Flow

1. WarehouseService creates/updates inventory → Publishes `BookStockUpdated` event
2. WarehouseService deletes inventory → Publishes `BookStockRemoved` event
3. SearchService consumes events → Updates search index with stock/price info
4. OrderService consumes events → Logs inventory changes for future validation
5. User searches → SearchService returns books with current stock and pricing

## Dependencies

- **SQL Server**: For storing inventory data (WarehouseItems table)
- **RabbitMQ**: For publishing stock update events
- **SearchService**: Consumes stock events for search result enrichment
- **OrderService**: Consumes stock events for inventory awareness
- **BookService**: Reference for book information validation
- **UserService**: Reference for seller information validation

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build warehouseservice

# Run the service
docker-compose up warehouseservice
```

The service will be available at `http://localhost:5001`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)

### Database Migration

The service automatically runs EF Core migrations on startup and seeds initial data with:
- 106,691 warehouse items
- 31,852 new books from GT-Library
- 74,839 used books from 100 student sellers

## Testing

### Using .http File

The service includes `WarehouseService.http` for testing endpoints:

```http
### Get All Items
GET http://localhost:5001/api/warehouse/items

### Get Items by ISBN
GET http://localhost:5001/api/warehouse/items/0439136350

### Get Items by Seller
GET http://localhost:5001/api/warehouse/sellers/GT-Library/items

### Get New Books Only
GET http://localhost:5001/api/warehouse/items/new

### Get Used Books Only
GET http://localhost:5001/api/warehouse/items/used

### Create New Book Item (GT-Library)
POST http://localhost:5001/api/warehouse/items
Content-Type: application/json

{
  "bookISBN": "1234567890123",
  "sellerId": "GT-Library",
  "quantity": 15,
  "price": 29.99,
  "location": "Main Warehouse",
  "isNew": true
}

### Create Used Book Item (Student Seller)
POST http://localhost:5001/api/warehouse/items
Content-Type: application/json

{
  "bookISBN": "0987654321",
  "sellerId": "student-c1235296",
  "quantity": 3,
  "price": 18.50,
  "location": "Campus-2",
  "isNew": false
}

### Update Item
PUT http://localhost:5001/api/warehouse/items/1
Content-Type: application/json

{
  "quantity": 8,
  "price": 22.99,
  "location": "Campus-3",
  "isNew": false
}

### Adjust Stock
POST http://localhost:5001/api/warehouse/adjust-stock
Content-Type: application/json

{
  "bookISBN": "0439136350",
  "sellerId": "student-c1235296",
  "quantityChange": -2
}

### Delete Item
DELETE http://localhost:5001/api/warehouse/items/1
```

### Manual Testing with curl

```bash
# Get all inventory
curl http://localhost:5001/api/warehouse/items

# Get inventory for specific book
curl http://localhost:5001/api/warehouse/items/0439136350

# Get inventory for specific seller
curl http://localhost:5001/api/warehouse/sellers/GT-Library/items

# Create new book inventory item
curl -X POST http://localhost:5001/api/warehouse/items \
  -H "Content-Type: application/json" \
  -d '{
    "bookISBN": "1234567890123",
    "sellerId": "GT-Library",
    "quantity": 15,
    "price": 29.99,
    "location": "Main Warehouse",
    "isNew": true
  }'

# Create used book inventory item
curl -X POST http://localhost:5001/api/warehouse/items \
  -H "Content-Type: application/json" \
  -d '{
    "bookISBN": "0987654321",
    "sellerId": "student-c1235296",
    "quantity": 3,
    "price": 18.50,
    "location": "Campus-2",
    "isNew": false
  }'

# Adjust stock
curl -X POST http://localhost:5001/api/warehouse/adjust-stock \
  -H "Content-Type: application/json" \
  -d '{
    "bookISBN": "0439136350",
    "sellerId": "student-c1235296",
    "quantityChange": -1
  }'

# Health check
curl http://localhost:5001/health
```

### Event Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify `BookStockUpdated` and `BookStockRemoved` events are published when inventory changes.

### Database Testing

Connect to SQL Server to verify WarehouseItems table:

```sql
SELECT * FROM WarehouseItems;
SELECT * FROM WarehouseItems WHERE BookISBN = '0439136350';
SELECT COUNT(*) as TotalItems FROM WarehouseItems;
SELECT COUNT(*) as NewBooks FROM WarehouseItems WHERE IsNew = 1;
SELECT COUNT(*) as UsedBooks FROM WarehouseItems WHERE IsNew = 0;
```

### Integration Testing

1. **Create Inventory**: Verify `BookStockUpdated` event is published
2. **Check Search Service**: Verify search results include stock/price information
3. **Adjust Stock**: Verify event is published and search index updates
4. **Delete Inventory**: Verify `BookStockRemoved` event is published
5. **Order Creation**: Verify OrderService logs stock changes

### Stock Adjustment Scenarios

- **Positive Adjustment**: `quantityChange: 5` (add 5 books)
- **Negative Adjustment**: `quantityChange: -3` (remove 3 books)
- **Zero Prevention**: Negative adjustments won't go below 0
- **Event Publishing**: All adjustments publish `BookStockUpdated` events
```

**Vigtigste ændringer:**
- ✅ **Fjernet** `Condition` fra alle eksempler og dokumentation
- ✅ **Tilføjet** `Location` og `IsNew` i alle eksempler
- ✅ **Tilføjet** nye endpoints i dokumentation
- ✅ **Opdateret** database model med nye kolonner
- ✅ **Tilføjet** `BookStockRemoved` event dokumentation
- ✅ **Opdateret** test eksempler med realistisk data fra dit datasæt