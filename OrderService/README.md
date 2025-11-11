# OrderService

## Description

The OrderService manages the complete order lifecycle in the Georgia Tech Library Marketplace. It handles order creation, payment processing, and status tracking while ensuring event-driven communication with other services. The service provides:

- **Order Management**: Create and track customer orders with multiple items
- **Payment Processing**: Handle order payments and status updates
- **Event Publishing**: Notify other services about order state changes
- **Stock Awareness**: Monitor inventory updates for order validation

The OrderService fits into the overall architecture as the commerce engine, coordinating between buyers, sellers, and inventory systems to facilitate book transactions.

## API Endpoints

### Create Order
- `POST /api/orders` - Create a new order

**Request Body:**
```json
{
  "customerId": "buyer123",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller456",
      "quantity": 2,
      "unitPrice": 22.99
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "buyer123",
  "orderDate": "2025-11-11T04:00:00Z",
  "totalAmount": 45.98,
  "status": "Pending",
  "orderItems": [
    {
      "orderItemId": "550e8400-e29b-41d4-a716-446655440001",
      "orderId": "550e8400-e29b-41d4-a716-446655440000",
      "bookISBN": "1234567890123",
      "sellerId": "seller456",
      "quantity": 2,
      "unitPrice": 22.99,
      "status": "Pending"
    }
  ]
}
```

### Get Order
- `GET /api/orders/{orderId}` - Retrieve order details

**Response (200 OK):** Order object as above

### Pay Order
- `POST /api/orders/{orderId}/pay` - Process payment for an order

**Request Body:**
```json
{
  "amount": 45.98
}
```

**Response (200 OK):**
```json
{
  "message": "Order paid successfully"
}
```

### Health Check
- `GET /health` - Service health status

## Database Model

### Orders Table

| Column | Type | Description |
|--------|------|-------------|
| OrderId | UNIQUEIDENTIFIER (PK) | Unique order identifier |
| CustomerId | NVARCHAR(100) | ID of the customer who placed the order |
| OrderDate | DATETIME | Date and time when order was created |
| TotalAmount | DECIMAL(18,2) | Total order amount |
| Status | NVARCHAR(20) | Order status (Pending, Paid, Shipped, Completed) |

### OrderItems Table

| Column | Type | Description |
|--------|------|-------------|
| OrderItemId | UNIQUEIDENTIFIER (PK) | Unique order item identifier |
| OrderId | UNIQUEIDENTIFIER (FK) | Reference to parent order |
| BookISBN | NVARCHAR(13) | ISBN of the ordered book |
| SellerId | NVARCHAR(100) | ID of the seller |
| Quantity | INT | Number of copies ordered |
| UnitPrice | DECIMAL(18,2) | Price per unit |
| Status | NVARCHAR(20) | Item status (Pending, Shipped) |

**Entity Diagram:**
```
Orders
├── OrderId: GUID (Primary Key)
├── CustomerId: String (Required, Max 100 chars)
├── OrderDate: DateTime (Required)
├── TotalAmount: Decimal (Required)
├── Status: String (Required, Max 20 chars)
└── OrderItems: ICollection<OrderItem>

OrderItems
├── OrderItemId: GUID (Primary Key)
├── OrderId: GUID (Foreign Key to Orders)
├── BookISBN: String (Required, Max 13 chars)
├── SellerId: String (Required, Max 100 chars)
├── Quantity: Int (Required, Min 1)
├── UnitPrice: Decimal (Required, Min 0.01)
└── Status: String (Required, Max 20 chars)
```

## Events

### Published Events

All events are published to the `book_events` exchange.

**OrderCreated** (Routing Key: `OrderCreated`)
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "buyer123",
  "orderDate": "2025-11-11T04:00:00Z",
  "totalAmount": 45.98,
  "orderItems": [
    {
      "orderItemId": "550e8400-e29b-41d4-a716-446655440001",
      "bookISBN": "1234567890123",
      "sellerId": "seller456",
      "quantity": 2,
      "unitPrice": 22.99
    }
  ]
}
```
*Published when:* A new order is created
*Consumed by:* NotificationService for seller notifications

**OrderPaid** (Routing Key: `OrderPaid`)
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "buyer123",
  "totalAmount": 45.98,
  "paidDate": "2025-11-11T04:05:00Z"
}
```
*Published when:* An order payment is processed
*Consumed by:* NotificationService to trigger seller shipping notifications

### Consumed Events

**BookStockUpdated** (Exchange: `book_events`, Routing Key: `BookStockUpdated`)
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
*Consumed when:* Warehouse inventory is updated
*Action:* Logs stock changes (future: validate order availability)

### Event Flow

1. OrderService creates order → Publishes `OrderCreated` event
2. NotificationService consumes `OrderCreated` → Stores order for notification
3. OrderService processes payment → Publishes `OrderPaid` event
4. NotificationService consumes `OrderPaid` → Sends shipping notifications to sellers
5. WarehouseService updates stock → Publishes `BookStockUpdated` event
6. OrderService consumes `BookStockUpdated` → Logs inventory changes

## Dependencies

- **SQL Server**: For storing order and order item data
- **RabbitMQ**: For publishing order events and consuming stock updates
- **WarehouseService**: Provides stock update events
- **NotificationService**: Consumes order events for seller notifications
- **BookService**: Reference for book information
- **AuthService**: For customer authentication

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build orderservice

# Run the service
docker-compose up orderservice
```

The service will be available at `http://localhost:5003`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)

### Database Migration

The service automatically runs EF Core migrations on startup and seeds initial data.

## Testing

### Using .http File

The service includes `OrderService.http` for testing endpoints:

```http
### Create Order
POST http://localhost:5003/api/orders
Content-Type: application/json

{
  "customerId": "buyer123",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller456",
      "quantity": 1,
      "unitPrice": 29.99
    }
  ]
}

### Get Order
GET http://localhost:5003/api/orders/550e8400-e29b-41d4-a716-446655440000

### Pay Order
POST http://localhost:5003/api/orders/550e8400-e29b-41d4-a716-446655440000/pay
Content-Type: application/json

{
  "amount": 29.99
}
```

### Manual Testing with curl

```bash
# Create an order
curl -X POST http://localhost:5003/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "buyer123",
    "orderItems": [
      {
        "bookISBN": "1234567890123",
        "sellerId": "seller456",
        "quantity": 1,
        "unitPrice": 29.99
      }
    ]
  }'

# Get order details
curl http://localhost:5003/api/orders/{order-id}

# Pay for order
curl -X POST http://localhost:5003/api/orders/{order-id}/pay \
  -H "Content-Type: application/json" \
  -d '{"amount": 29.99}'

# Health check
curl http://localhost:5003/health
```

### Event Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify event publishing when orders are created and paid.

### Database Testing

Connect to SQL Server to verify Orders and OrderItems tables:

```sql
SELECT * FROM Orders;
SELECT * FROM OrderItems;
```

### Integration Testing

1. **Create Order**: Verify order is created and `OrderCreated` event is published
2. **Check Notification Service**: Verify order is stored in NotificationService memory
3. **Pay Order**: Verify `OrderPaid` event is published and order status updates
4. **Check Notifications**: Verify sellers receive shipping notifications
