# NotificationService

## Description

The NotificationService is responsible for sending notifications to sellers in the Georgia Tech Library Marketplace. It operates as a background service that monitors order events and notifies sellers when their items have been purchased and paid for. The service handles:

- **Order Event Processing**: Listens for order creation and payment events
- **Seller Notifications**: Sends shipping notifications to sellers when orders are paid
- **In-Memory Order Tracking**: Temporarily stores order details until payment confirmation

The NotificationService fits into the overall architecture as the notification hub, ensuring sellers are informed about new orders requiring fulfillment. It acts as an event-driven component that reacts to order lifecycle events.

## API Endpoints

The NotificationService does not expose any HTTP API endpoints. It runs as a background console application that processes messages from RabbitMQ.

## Database Model

The NotificationService does not use a persistent database. It maintains an in-memory cache of orders using `ConcurrentDictionary<Guid, OrderCreatedEvent>` to track orders between creation and payment events.

**In-Memory Structure:**
```
_orders: Dictionary<Guid, OrderCreatedEvent>
├── Key: OrderId (GUID)
└── Value: OrderCreatedEvent
    ├── OrderId: GUID
    ├── CustomerId: String
    ├── OrderDate: DateTime
    ├── TotalAmount: Decimal
    └── OrderItems: List<OrderItemEvent>
        ├── OrderItemId: GUID
        ├── BookISBN: String
        ├── SellerId: String
        ├── Quantity: Int
        └── UnitPrice: Decimal
```

## Events

### Consumed Events

All events are consumed from the `book_events` exchange (note: despite the name, this exchange handles various events including orders).

**OrderCreated** (Routing Key: `OrderCreated`)
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "seller123",
  "orderDate": "2025-11-11T04:00:00Z",
  "totalAmount": 45.99,
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
*Consumed when:* An order is created
*Action:* Stores order in memory for later notification

**OrderPaid** (Routing Key: `OrderPaid`)
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "buyer123",
  "totalAmount": 45.99,
  "paidDate": "2025-11-11T04:05:00Z"
}
```
*Consumed when:* An order payment is confirmed
*Action:* Retrieves stored order and sends notifications to all sellers involved

### Event Flow

1. OrderService creates order → Publishes `OrderCreated` event
2. NotificationService consumes `OrderCreated` → Stores order in memory
3. OrderService processes payment → Publishes `OrderPaid` event
4. NotificationService consumes `OrderPaid` → Sends notifications to sellers
5. NotificationService cleans up order from memory

## Dependencies

- **RabbitMQ**: For consuming order events
- **OrderService**: Publishes order creation and payment events
- **External Notification Services**: (Future) Email/SMS gateways like SendGrid, Twilio

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build notificationservice

# Run the service
docker-compose up notificationservice
```

The service runs as a background console application and does not expose HTTP endpoints.

### Environment Variables

- `RabbitMQ__HostName`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)

## Testing

### Log Monitoring

The service logs all notification activities. Monitor the console output or logs for notification events:

```
📧 Sending notification to seller seller456: Order 550e8400-e29b-41d4-a716-446655440000 - Please ship 2x of book 1234567890123
```

### RabbitMQ Management

Access RabbitMQ management UI at `http://localhost:15672` to:

- Verify event consumption from the `book_events` exchange
- Check queue bindings for `OrderCreated` and `OrderPaid` routing keys
- Monitor message rates during testing

### Integration Testing

1. **Create an Order**: Use OrderService to create an order
2. **Verify Storage**: Check NotificationService logs for "Stored order {OrderId}..."
3. **Pay for Order**: Use OrderService to mark order as paid
4. **Verify Notification**: Check logs for seller notification messages
5. **Verify Cleanup**: Confirm order is removed from memory

### Manual Event Publishing

Use RabbitMQ management UI or CLI to publish test events:

```bash
# Publish OrderCreated event
rabbitmqadmin publish exchange=book_events routing_key=OrderCreated payload='{"orderId":"test-order-id","customerId":"buyer123","orderDate":"2025-11-11T04:00:00Z","totalAmount":29.99,"orderItems":[{"orderItemId":"item1","bookISBN":"1234567890123","sellerId":"seller456","quantity":1,"unitPrice":29.99}]}'

# Publish OrderPaid event
rabbitmqadmin publish exchange=book_events routing_key=OrderPaid payload='{"orderId":"test-order-id","customerId":"buyer123","totalAmount":29.99,"paidDate":"2025-11-11T04:05:00Z"}'
```

### Expected Behavior

- **OrderCreated**: Service logs storage of order with item count
- **OrderPaid**: Service sends notification to seller and logs cleanup
- **Unknown Order Paid**: Service logs warning for orders not found in memory

### Load Testing

The service uses in-memory storage, so test scenarios with multiple concurrent orders to ensure thread safety with `ConcurrentDictionary`.
