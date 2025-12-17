# Checkout Flow - Backend Perspektiv
## Komplet Flow Dokumentation med Kode-Referencer

Dette dokument beskriver det komplette checkout-flow fra backend-perspektiv, fra når API Gateway modtager en checkout request til ordren er fuldt behandlet og sælgere notificeret.

---

## Flow Oversigt

```
Frontend → API Gateway → OrderService (ShoppingCartController)
    ↓
ShoppingCartService.ConvertCartToOrderAsync()
    ↓
PaymentService.ProcessPaymentAsync() [Mock]
    ↓
OrderService.CreateOrderWithPaymentAsync()
    ↓
Order.CreatePaid() [Domain Entity]
    ↓
OrderRepository.CreateAsync() [Database]
    ↓
PublishOrderCreatedEventAsync() [RabbitMQ]
    ↓
PublishOrderPaidEventAsync() [RabbitMQ]
    ↓
┌─────────────────────────────────────────┐
│         ASYNKRONE EVENT FLOW             │
│         (SAGA Pattern)                   │
└─────────────────────────────────────────┘
    ↓
┌─────────────────┬─────────────────┬─────────────────┬─────────────────┐
│ WarehouseService│ SearchService   │ UserService     │ NotificationService│
│ (OrderPaid)     │ (BookStockUpdated)│ (OrderPaid)    │ (OrderPaid)      │
│                 │                 │                 │                  │
│ - Reducer stock │ - Opdater Redis │ - Opdater seller│ - Send emails    │
│ - Publiser      │   cache         │   stats         │   til sælgere   │
│   BookStockUpdated│                 │                 │                  │
└─────────────────┴─────────────────┴─────────────────┴─────────────────┘
```

---

## Step 1: API Gateway Modtager POST /api/cart/{customerId}/checkout

### Controller: ShoppingCartController
**Fil:** `OrderService/API/Controllers/ShoppingCartController.cs`

**Endpoint:**
```csharp
[HttpPost("{customerId}/checkout")]
[Consumes("application/json")]
[Produces("application/json")]
public async Task<ActionResult<OrderDto>> Checkout(
    string customerId,
    [FromBody] CheckoutDto checkoutDto)
```

**Request Validation:**
```csharp
// Linje 155-172: ModelState validation
if (!ModelState.IsValid)
{
    return BadRequest(new { 
        error = "Validation failed", 
        errors = ModelState.ToDictionary(...)
    });
}

// Linje 174-187: DeliveryAddress validation
if (checkoutDto.DeliveryAddress == null)
{
    return BadRequest(new { 
        error = "Delivery address is required" 
    });
}
```

**Service Call:**
```csharp
// Linje 195-199: Kalder ShoppingCartService
var order = await _cartService.ConvertCartToOrderAsync(
    customerId, 
    checkoutDto.DeliveryAddress, 
    checkoutDto.Amount, 
    checkoutDto.PaymentMethod);
```

**Pattern:** RESTful API, Controller-Service pattern

---

## Step 2: ShoppingCartService Konverterer Cart til Ordre

### Service: ShoppingCartService
**Fil:** `OrderService/Application/Services/ShoppingCartService.cs`

**Metode:** `ConvertCartToOrderAsync`

**Step 2.1: Valider Cart**
```csharp
// Linje 118-131: Hent og valider cart
var cart = await _cartRepository.GetByCustomerIdAsync(customerId);
if (cart == null || cart.IsEmpty())
{
    throw new ShoppingCartException("Cannot create order from empty cart");
}
```

**Step 2.2: Valider Payment Amount**
```csharp
// Linje 133-143: Tjek at payment amount matcher cart total
var cartTotal = cart.CalculateTotal().Amount;
if (paymentAmount != cartTotal)
{
    throw new InvalidPaymentException(
        $"Payment amount {paymentAmount} does not match cart total {cartTotal}");
}
```

**Step 2.3: Processer Betaling**
```csharp
// Linje 145-180: Processer betaling gennem PaymentService
var paymentResult = await _paymentService.ProcessPaymentAsync(
    Guid.NewGuid(), // Temporary order ID
    paymentAmount,
    paymentMethod);

if (!paymentResult.Success)
{
    throw new InvalidPaymentException($"Payment failed: {paymentResult.Message}");
}
```

**Step 2.4: Opret CreateOrderDto**
```csharp
// Linje 191-202: Konverter cart items til order items
var createOrderDto = new CreateOrderDto
{
    CustomerId = customerId,
    OrderItems = cart.Items.Select(item => new CreateOrderItemDto
    {
        BookISBN = item.BookISBN,
        SellerId = item.SellerId,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice.Amount
    }).ToList(),
    DeliveryAddress = deliveryAddress
};
```

**Step 2.5: Opret Ordre med Paid Status**
```csharp
// Linje 206-210: Kalder OrderService.CreateOrderWithPaymentAsync
var createdOrder = await _orderService.CreateOrderWithPaymentAsync(
    createOrderDto, 
    paymentAmount, 
    paymentResult.TransactionId);
```

**Step 2.6: Ryd Cart**
```csharp
// Linje 213-216: Tøm cart efter succesfuld ordre
cart.Clear();
await _cartRepository.UpdateAsync(cart);
```

**Pattern:** Service Layer, Transaction Script

---

## Step 3: PaymentService Processerer Betaling

### Service: MockPaymentService
**Fil:** `OrderService/Infrastructure/Payment/MockPaymentService.cs`

**Metode:** `ProcessPaymentAsync`

```csharp
// Linje 18-70: Mock payment processing
public Task<PaymentResult> ProcessPaymentAsync(
    Guid orderId, 
    decimal amount, 
    string paymentMethod = "card")
{
    // Valider payment method
    if (amount <= 0)
    {
        return Task.FromResult(new PaymentResult
        {
            Success = false,
            Message = $"Invalid payment amount: {amount}"
        });
    }

    // Simuler succesfuld betaling
    var result = new PaymentResult
    {
        Success = true,
        TransactionId = $"MOCK-{Guid.NewGuid():N}",
        Message = "Payment processed successfully (mock)",
        ProcessedAt = DateTime.UtcNow,
        Amount = amount
    };

    _paymentHistory[orderId] = result;
    return Task.FromResult(result);
}
```

**Pattern:** Strategy Pattern (IPaymentService interface), Mock Implementation

**Note:** I produktion ville dette være en integration til Stripe, PayPal, eller lignende payment gateway.

---

## Step 4: OrderService Opretter Ordre med Paid Status

### Service: OrderService
**Fil:** `OrderService/Application/Services/OrderService.cs`

**Metode:** `CreateOrderWithPaymentAsync`

**Step 4.1: Valider Input**
```csharp
// Linje 139-150: Valider at sælger ikke køber egne bøger
var conflictingItems = createOrderDto.OrderItems
    .Where(item => item.SellerId.Equals(createOrderDto.CustomerId, 
        StringComparison.OrdinalIgnoreCase))
    .ToList();

if (conflictingItems.Any())
{
    throw new ValidationException("Sellers cannot buy their own books");
}
```

**Step 4.2: Opret OrderItems**
```csharp
// Linje 152-165: Opret OrderItem entities fra DTO
var orderItems = createOrderDto.OrderItems
    .Select(item => OrderItem.Create(
        item.BookISBN,
        item.SellerId,
        item.Quantity,
        item.UnitPrice))
    .ToList();
```

**Step 4.3: Opret Order Entity med Paid Status**
```csharp
// Linje 167-170: Opret Order med Paid status direkte
var order = Order.CreatePaid(
    createOrderDto.CustomerId, 
    orderItems, 
    deliveryAddress, 
    paymentAmount);
```

**Step 4.4: Gem i Database**
```csharp
// Linje 172-174: Gem ordre i database
var createdOrder = await _orderRepository.CreateAsync(order);
```

**Pattern:** Domain-Driven Design (DDD), Repository Pattern

---

## Step 5: Order Domain Entity - CreatePaid Factory Method

### Entity: Order
**Fil:** `OrderService/Domain/Entities/Order.cs`

**Factory Method:**
```csharp
// Linje 79-101: CreatePaid factory method
public static Order CreatePaid(
    string customerId, 
    List<OrderItem> orderItems, 
    Address deliveryAddress, 
    decimal paymentAmount)
{
    ValidateCustomerId(customerId);
    ValidateOrderItems(orderItems);
    
    if (deliveryAddress == null)
        throw new ArgumentNullException(nameof(deliveryAddress));

    var order = new Order(
        Guid.NewGuid(),
        customerId,
        DateTime.UtcNow,
        orderItems,
        deliveryAddress,
        OrderStatus.Paid,  // Initial status er Paid
        DateTime.UtcNow);  // PaidDate sættes nu

    // Valider at payment amount matcher order total
    if (paymentAmount != order.TotalAmount.Amount)
        throw new InvalidPaymentException(
            order.TotalAmount.Amount, paymentAmount);

    return order;
}
```

**Initial Status:** `OrderStatus.Paid` (ikke Pending, da betaling allerede er processeret)

**Pattern:** Domain Model, Factory Pattern, Rich Domain Model

---

## Step 6: Event Publishing - OrderCreated og OrderPaid

### Service: OrderService
**Fil:** `OrderService/Application/Services/OrderService.cs`

**Step 6.1: Publiser OrderCreated Event**
```csharp
// Linje 185-186: Publiser OrderCreated event
await PublishOrderCreatedEventAsync(createdOrder);
```

**Event Publishing Implementation:**
```csharp
// Linje 426-470: PublishOrderCreatedEventAsync
private async Task PublishOrderCreatedEventAsync(Order order)
{
    var orderEvent = new
    {
        OrderId = order.OrderId,
        CustomerId = order.CustomerId,
        OrderDate = order.OrderDate,
        TotalAmount = order.TotalAmount.Amount,
        PaymentStatus = order.Status.ToString(), // "Paid"
        PaidDate = order.PaidDate,
        OrderItems = order.OrderItems.Select(item => new
        {
            OrderItemId = item.OrderItemId,
            BookISBN = item.BookISBN,
            SellerId = item.SellerId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice.Amount
        }).ToList()
    };

    await _messageProducer.SendMessageAsync(orderEvent, "OrderCreated");
}
```

**Step 6.2: Publiser OrderPaid Event**
```csharp
// Linje 188-190: Publiser OrderPaid event (triggerer stock reduction)
await PublishOrderPaidEventAsync(createdOrder);
```

**Event Publishing Implementation:**
```csharp
// Linje 472-521: PublishOrderPaidEventAsync
private async Task PublishOrderPaidEventAsync(Order order)
{
    var orderEvent = new
    {
        OrderId = order.OrderId,
        CustomerId = order.CustomerId,
        TotalAmount = order.TotalAmount.Amount,
        PaidDate = order.PaidDate,
        OrderItems = order.OrderItems.Select(item => new
        {
            OrderItemId = item.OrderItemId,
            BookISBN = item.BookISBN,
            SellerId = item.SellerId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice.Amount
        }).ToList()
    };

    await _messageProducer.SendMessageAsync(orderEvent, "OrderPaid");
}
```

**RabbitMQ Producer:**
**Fil:** `OrderService/Infrastructure/Messaging/RabbitMQProducer.cs`

```csharp
// Linje 99-103: SendMessageAsync
public Task SendMessageAsync<T>(T message, string routingKey)
{
    var json = JsonSerializer.Serialize(message);
    var body = Encoding.UTF8.GetBytes(json);
    
    var properties = _channel.CreateBasicProperties();
    properties.Persistent = true;
    properties.ContentType = "application/json";
    
    _channel.BasicPublish(
        exchange: "book_events",
        routingKey: routingKey,
        basicProperties: properties,
        body: body);
    
    return Task.CompletedTask;
}
```

**Pattern:** Event-Driven Architecture, Message Queue (RabbitMQ), Publisher-Subscriber

**Note om Outbox Pattern:**
I nuværende implementering publiceres events direkte til RabbitMQ. I produktion bør man implementere **Outbox Pattern** for at sikre atomisk event publishing:

```csharp
// PSEUDO-KODE - Outbox Pattern (planlagt)
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try
{
    // 1. Gem ordre i database
    await _orderRepository.CreateAsync(order);
    
    // 2. Gem event i Outbox table (samme transaction)
    var outboxEvent = new OutboxEvent
    {
        EventId = Guid.NewGuid(),
        EventType = "OrderPaid",
        Payload = JsonSerializer.Serialize(orderEvent),
        Status = "Pending",
        CreatedAt = DateTime.UtcNow
    };
    await _outboxRepository.AddAsync(outboxEvent);
    
    // 3. Commit transaction (atomisk)
    await transaction.CommitAsync();
    
    // 4. Background worker publicerer events fra Outbox
    // (separat process der læser Pending events og sender til RabbitMQ)
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## Step 7: WarehouseService Modtager OrderPaid Event

### Consumer: RabbitMQConsumer
**Fil:** `WarehouseService/Services/RabbitMQConsumer.cs`

**Event Handler:**
```csharp
// Linje 166-346: HandleOrderPaidAsync
private async Task HandleOrderPaidAsync(string message)
{
    // Step 1: Deserialiser event
    var orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message);
    
    // Step 2: Opret service scope
    using var scope = _serviceProvider.CreateScope();
    var warehouseRepository = scope.ServiceProvider
        .GetRequiredService<IWarehouseItemRepository>();
    
    // Step 3: Processer hvert order item
    foreach (var orderItem in orderEvent.OrderItems)
    {
        // 3.1: Find warehouse item
        var warehouseItem = await warehouseRepository
            .GetWarehouseItemByBookAndSellerAsync(
                orderItem.BookISBN,
                orderItem.SellerId);
        
        if (warehouseItem == null)
        {
            _logger.LogWarning("Warehouse item not found");
            continue;
        }
        
        // 3.2: Reducer stock
        var oldQuantity = warehouseItem.Quantity;
        warehouseItem.Quantity -= orderItem.Quantity;
        
        if (warehouseItem.Quantity < 0)
        {
            warehouseItem.Quantity = 0; // Prevent negative stock
        }
        
        // 3.3: Opdater i database
        var updatedItem = await warehouseRepository
            .UpdateWarehouseItemAsync(warehouseItem.Id, warehouseItem);
        
        // 3.4: Publiser BookStockUpdated event (aggregated)
        var stockAggregationService = scope.ServiceProvider
            .GetRequiredService<StockAggregationService>();
        await stockAggregationService
            .PublishAggregatedStockEventAsync(updatedItem.BookISBN);
    }
}
```

**Stock Aggregation Service:**
**Fil:** `WarehouseService/Services/StockAggregationService.cs`

```csharp
// Linje 27-123: PublishAggregatedStockEventAsync
public async Task PublishAggregatedStockEventAsync(string bookISBN)
{
    // Hent alle warehouse items for ISBN
    var allItems = await _warehouseRepository
        .GetWarehouseItemsByBookIsbnAsync(bookISBN);
    
    // Filtrer items med quantity > 0
    var availableItems = allItems
        .Where(item => item.Quantity > 0)
        .ToList();
    
    // Beregn aggregerede værdier
    var totalStock = availableItems.Sum(item => item.Quantity);
    var availableSellers = availableItems
        .Select(item => item.SellerId)
        .Distinct()
        .Count();
    var minPrice = availableItems.Min(item => item.Price);
    var maxPrice = availableItems.Max(item => item.Price);
    var averagePrice = availableItems.Average(item => item.Price);
    
    // Opret seller entries
    var sellers = availableItems.Select(item => new
    {
        SellerId = item.SellerId,
        Price = item.Price,
        Quantity = item.Quantity,
        Condition = item.IsNew ? "New" : "Used",
        Location = item.Location
    }).ToList();
    
    // Publiser aggregated event
    var aggregatedEvent = new
    {
        BookISBN = bookISBN,
        TotalStock = totalStock,
        AvailableSellers = availableSellers,
        MinPrice = minPrice,
        MaxPrice = maxPrice,
        AveragePrice = averagePrice,
        UpdatedAt = DateTime.UtcNow,
        Sellers = sellers
    };
    
    _messageProducer.SendMessage(aggregatedEvent, "BookStockUpdated");
}
```

**Pattern:** Event-Driven Architecture, SAGA Pattern, Event Sourcing (implicit), Aggregation Pattern

---

## Step 8: SearchService Modtager BookStockUpdated Event

### Consumer: BookEventConsumer
**Fil:** `SearchService/Infrastructure/Messaging/RabbitMQ/BookEventConsumer.cs`

**Event Handler:**
```csharp
// Linje 261-311: HandleBookStockUpdatedAsync
private async Task HandleBookStockUpdatedAsync(
    IMediator mediator, 
    string message, 
    CancellationToken cancellationToken)
{
    // Deserialiser event
    var stockEvent = JsonSerializer.Deserialize<StockEventDto>(message);
    
    // Opret command med aggregated data fra event
    var command = new UpdateBookStockCommand(
        stockEvent.BookISBN,
        stockEvent.TotalStock,
        stockEvent.AvailableSellers,
        stockEvent.MinPrice,
        stockEvent.Sellers);
    
    // Send command gennem Mediator (CQRS)
    var result = await mediator.Send(command, cancellationToken);
    
    if (result.Success)
    {
        _logger.LogInformation(
            "Successfully updated stock for book ISBN {ISBN}", 
            stockEvent.BookISBN);
    }
}
```

**Command Handler:**
**Fil:** `SearchService/Application/Commands/Stock/UpdateBookStockCommandHandler.cs`

```csharp
// Linje 27-61: Handle method
public async Task<UpdateBookStockResult> Handle(
    UpdateBookStockCommand request, 
    CancellationToken cancellationToken)
{
    // Hent bog fra Redis
    var isbn = ISBN.Create(request.BookISBN);
    var book = await _repository.GetByIsbnAsync(isbn, cancellationToken);
    
    if (book == null)
    {
        return new UpdateBookStockResult(false, "Book not found");
    }
    
    // Opdater stock information
    book.UpdateStock(
        request.TotalStock, 
        request.AvailableSellers, 
        request.MinPrice);
    
    // Gem i Redis
    await _repository.AddOrUpdateAsync(book, cancellationToken);
    
    // Opdater sellers data i Redis
    await UpdateSellersDataAsync(
        request.BookISBN, 
        request.Sellers, 
        cancellationToken);
    
    // Ryd page caches (stock ændret)
    await _cache.RemoveByPatternAsync("available:page:*", cancellationToken);
    
    return new UpdateBookStockResult(true);
}
```

**Redis Update:**
```csharp
// Linje 66-70: UpdateSellersDataAsync
private async Task UpdateSellersDataAsync(
    string bookISBN, 
    List<SellerInfoDto>? sellers, 
    CancellationToken cancellationToken)
{
    var sellersKey = $"sellers:{bookISBN}";
    var sellersJson = JsonSerializer.Serialize(sellers ?? new List<SellerInfoDto>());
    await _cache.SetAsync(sellersKey, sellersJson, cancellationToken);
}
```

**Pattern:** CQRS (Command Query Responsibility Segregation), Mediator Pattern, Cache-Aside Pattern, Event-Driven Architecture

---

## Step 9: UserService Modtager OrderPaid Event

### Consumer: OrderEventConsumer
**Fil:** `UserService/Infrastructure/Messaging/OrderEventConsumer.cs`

**Event Handler:**
```csharp
// Linje 316-428: HandleOrderPaidAsync
private async Task HandleOrderPaidAsync(
    string message, 
    CancellationToken cancellationToken)
{
    // Deserialiser event
    var orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message);
    
    // Opret service scope
    using var scope = _serviceProvider.CreateScope();
    var sellerService = scope.ServiceProvider
        .GetRequiredService<ISellerService>();
    
    // Processer hvert order item
    foreach (var orderItem in orderEvent.OrderItems)
    {
        // Parse SellerId
        if (!Guid.TryParse(orderItem.SellerId, out var sellerId))
        {
            continue;
        }
        
        // Opdater listing quantity og opret BookSale record
        await sellerService.UpdateListingQuantityFromOrderAsync(
            orderEvent.OrderId,
            orderItem.OrderItemId,
            orderEvent.CustomerId,
            sellerId,
            orderItem.BookISBN,
            condition: null,
            orderItem.Quantity,
            orderItem.UnitPrice,
            cancellationToken);
    }
}
```

**Seller Service:**
**Fil:** `UserService/Application/Services/SellerService.cs`

```csharp
// Linje 288-291: UpdateListingQuantityFromOrderAsync
public async Task UpdateListingQuantityFromOrderAsync(
    Guid orderId,
    Guid orderItemId,
    string buyerId,
    Guid sellerId,
    string bookISBN,
    string? condition,
    int quantitySold,
    decimal unitPrice,
    CancellationToken cancellationToken = default)
{
    // Find listing
    var listing = await _listingRepository
        .GetByBookISBNAndSellerAsync(bookISBN, sellerId, cancellationToken);
    
    if (listing == null)
    {
        _logger.LogWarning("Listing not found");
        return;
    }
    
    // Reducer quantity
    listing.ReduceQuantity(quantitySold);
    
    // Hvis quantity = 0, markér som sold
    if (listing.Quantity == 0)
    {
        listing.MarkAsSold(orderId, orderItemId, buyerId, DateTime.UtcNow);
        // Dette triggerer automatisk BookSold event
    }
    
    // Opret BookSale record
    var bookSale = BookSale.Create(
        orderId,
        orderItemId,
        listing.ListingId,
        sellerId,
        buyerId,
        bookISBN,
        quantitySold,
        unitPrice,
        DateTime.UtcNow);
    
    await _bookSaleRepository.AddAsync(bookSale, cancellationToken);
    
    // Opdater seller stats
    await UpdateSellerStatsFromOrderAsync(
        sellerId, 
        quantitySold, 
        null, 
        cancellationToken);
    
    await _listingRepository.UpdateAsync(listing, cancellationToken);
}
```

**Update Seller Stats:**
```csharp
// Linje 269-286: UpdateSellerStatsFromOrderAsync
public async Task UpdateSellerStatsFromOrderAsync(
    Guid sellerId, 
    int booksSold, 
    decimal? orderRating, 
    CancellationToken cancellationToken = default)
{
    var sellerProfile = await _sellerRepository
        .GetByIdAsync(sellerId, cancellationToken);
    
    if (sellerProfile == null)
    {
        return;
    }
    
    // Opdater stats i domain entity
    sellerProfile.UpdateFromOrder(booksSold, orderRating);
    
    // Gem i database
    await _sellerRepository.UpdateAsync(sellerProfile, cancellationToken);
    
    // Publiser SellerUpdated event
    PublishSellerUpdatedEvent(sellerProfile);
}
```

**Pattern:** Event-Driven Architecture, Domain Events, SAGA Pattern

---

## Step 10: NotificationService Modtager OrderPaid Event

### Consumer: RabbitMQConsumer
**Fil:** `NotificationService/Infrastructure/Messaging/RabbitMQConsumer.cs`

**Event Handler:**
```csharp
// Linje 166-190: HandleOrderPaidAsync
private async Task HandleOrderPaidAsync(
    INotificationService notificationService, 
    string message)
{
    // Deserialiser event
    var orderEvent = JsonSerializer.Deserialize<OrderPaidEvent>(message);
    
    // Send bekræftelsesemail til kunde
    var customerNotification = new CreateNotificationDto
    {
        RecipientId = orderEvent.CustomerId,
        RecipientEmail = $"{orderEvent.CustomerId}@example.com", // TODO: Get actual email
        Type = NotificationType.OrderPaid.ToString(),
        Subject = $"Payment Confirmed - Order #{orderEvent.OrderId}",
        Message = $"Your payment has been successfully processed!\n\n" +
                  $"Order ID: {orderEvent.OrderId}\n" +
                  $"Amount Paid: ${orderEvent.TotalAmount:F2}\n" +
                  $"Paid Date: {orderEvent.PaidDate:yyyy-MM-dd HH:mm}\n\n" +
                  $"Your order will be shipped soon.",
        Metadata = new Dictionary<string, string>
        {
            ["OrderId"] = orderEvent.OrderId.ToString(),
            ["TotalAmount"] = orderEvent.TotalAmount.ToString("F2")
        }
    };
    
    var notification = await notificationService
        .CreateNotificationAsync(customerNotification);
    await notificationService.SendNotificationAsync(notification.NotificationId);
}
```

**Notification Service:**
**Fil:** `NotificationService/Application/Services/NotificationService.cs`

```csharp
// Linje 91-128: SendNotificationAsync
public async Task SendNotificationAsync(Guid notificationId)
{
    var notification = await _notificationRepository
        .GetByIdAsync(notificationId);
    
    if (notification == null)
        throw new NotificationNotFoundException(notificationId);
    
    // Tjek user preferences
    var preferences = await _preferenceRepository
        .GetOrCreateForUserAsync(notification.RecipientId);
    
    if (!preferences.IsEnabled(notification.Type))
    {
        _logger.LogInformation(
            "Notification {NotificationId} skipped due to user preferences", 
            notificationId);
        return;
    }
    
    try
    {
        // Send email (mock implementation)
        var result = await _emailService.SendEmailAsync(
            notification.RecipientEmail,
            notification.Subject,
            notification.Message,
            notification.Message);
        
        if (result.Success)
        {
            notification.MarkAsSent();
            _logger.LogInformation(
                "Notification {NotificationId} sent successfully", 
                notificationId);
        }
        else
        {
            notification.MarkAsFailed(result.Message);
        }
    }
    catch (Exception ex)
    {
        notification.MarkAsFailed(ex.Message);
        _logger.LogError(ex, "Error sending notification {NotificationId}", notificationId);
    }
    
    await _notificationRepository.UpdateAsync(notification);
}
```

**Email Service (Mock):**
**Fil:** `NotificationService/Infrastructure/Email/MockEmailService.cs`

```csharp
public async Task<EmailResult> SendEmailAsync(
    string to, 
    string subject, 
    string htmlBody, 
    string textBody)
{
    // Mock implementation - logger email i stedet for at sende
    _logger.LogInformation(
        "Sending email to {To}, Subject: {Subject}", 
        to, subject);
    
    return new EmailResult
    {
        Success = true,
        Message = "Email sent successfully (mock)"
    };
}
```

**Pattern:** Event-Driven Architecture, Strategy Pattern (EmailService interface), User Preferences Pattern

---

## Integration Patterns og Trade-offs

### 1. Synkron vs. Asynkron Kommunikation

**Synkron (HTTP):**
- **OrderService → PaymentService:** Synkron (betaling skal bekræftes før ordre oprettes)
- **OrderService → WarehouseService (stock check):** Ikke implementeret endnu, men ville være synkron

**Asynkron (RabbitMQ Events):**
- **OrderService → WarehouseService:** Asynkron (OrderPaid event)
- **WarehouseService → SearchService:** Asynkron (BookStockUpdated event)
- **OrderService → UserService:** Asynkron (OrderPaid event)
- **OrderService → NotificationService:** Asynkron (OrderPaid event)

**Trade-off:** 
- **Synkron:** Simpelt, men kan blive bottleneck
- **Asynkron:** Skalerbart, men eventual consistency

### 2. Database per Service

Hver service har sin egen database:
- **OrderService:** SQL Server (Orders, OrderItems)
- **WarehouseService:** SQL Server (WarehouseItems)
- **SearchService:** Redis (BookSearchModel)
- **UserService:** SQL Server (Sellers, BookListings, BookSales)
- **NotificationService:** SQL Server (Notifications)

**Konsekvens:** Ingen direkte joins mellem services - kommunikation sker gennem events eller API calls.

### 3. Eventual Consistency

**Eksempel:** Når en ordre betales:
1. OrderService opdaterer ordre status til Paid (immediate)
2. WarehouseService reducerer stock (eventual - via event)
3. SearchService opdaterer Redis cache (eventual - via event)
4. UserService opdaterer seller stats (eventual - via event)

**Trade-off:** Systemet kan være i inkonsistent tilstand i korte perioder, men er mere skalerbart.

### 4. SAGA Pattern

Checkout-flowet implementerer en **Choreography-based SAGA**:

```
OrderPaid Event
    ↓
┌──────────────────────────────────────┐
│  WarehouseService: Reducer stock      │
│  UserService: Opdater seller stats   │
│  NotificationService: Send emails    │
│  SearchService: Opdater cache        │
└──────────────────────────────────────┘
```

Hver service håndterer sin del uafhængigt. Hvis en service fejler, kan man implementere compensation (rollback).

**Compensation (ikke implementeret endnu):**
- Hvis stock reduction fejler → Publiser OrderCancelled event
- Hvis seller stats update fejler → Log error, retry senere
- Hvis email sending fejler → Retry queue

### 5. Outbox Pattern (Planlagt)

**Problem:** Hvis OrderService crasher efter at gemme ordre, men før event publiceres, mister vi eventet.

**Løsning:** Outbox Pattern
1. Gem ordre + event i samme database transaction
2. Background worker læser events fra Outbox og publicerer til RabbitMQ
3. Markér event som "Published" når sendt

**Implementering (pseudo-kode):**
```csharp
// I OrderService.CreateOrderWithPaymentAsync
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try
{
    // 1. Gem ordre
    var order = await _orderRepository.CreateAsync(order);
    
    // 2. Gem event i Outbox (samme transaction)
    var outboxEvent = new OutboxEvent
    {
        EventId = Guid.NewGuid(),
        EventType = "OrderPaid",
        Payload = JsonSerializer.Serialize(orderEvent),
        Status = "Pending",
        CreatedAt = DateTime.UtcNow
    };
    await _outboxRepository.AddAsync(outboxEvent);
    
    // 3. Commit (atomisk)
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// Background worker (separat process)
public class OutboxPublisher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingEvents = await _outboxRepository
                .GetPendingEventsAsync();
            
            foreach (var evt in pendingEvents)
            {
                try
                {
                    await _messageProducer
                        .SendMessageAsync(evt.Payload, evt.EventType);
                    
                    evt.MarkAsPublished();
                    await _outboxRepository.UpdateAsync(evt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish event");
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

---

## Fil-Referencer

### OrderService
- `OrderService/API/Controllers/ShoppingCartController.cs` - Checkout endpoint
- `OrderService/Application/Services/ShoppingCartService.cs` - Cart to order conversion
- `OrderService/Application/Services/OrderService.cs` - Order creation and event publishing
- `OrderService/Domain/Entities/Order.cs` - Order domain entity
- `OrderService/Infrastructure/Payment/MockPaymentService.cs` - Payment processing
- `OrderService/Infrastructure/Messaging/RabbitMQProducer.cs` - Event publishing

### WarehouseService
- `WarehouseService/Services/RabbitMQConsumer.cs` - OrderPaid event consumer
- `WarehouseService/Services/StockAggregationService.cs` - Stock aggregation and BookStockUpdated event

### SearchService
- `SearchService/Infrastructure/Messaging/RabbitMQ/BookEventConsumer.cs` - BookStockUpdated event consumer
- `SearchService/Application/Commands/Stock/UpdateBookStockCommandHandler.cs` - Redis cache update

### UserService
- `UserService/Infrastructure/Messaging/OrderEventConsumer.cs` - OrderPaid event consumer
- `UserService/Application/Services/SellerService.cs` - Seller stats update

### NotificationService
- `NotificationService/Infrastructure/Messaging/RabbitMQConsumer.cs` - OrderPaid event consumer
- `NotificationService/Application/Services/NotificationService.cs` - Email sending
- `NotificationService/Infrastructure/Email/MockEmailService.cs` - Mock email service

---

## Konklusion

Checkout-flowet implementerer en **event-driven microservices arkitektur** med:

1. **Synkron betaling:** PaymentService valideres før ordre oprettes
2. **Asynkron SAGA:** OrderPaid event triggerer multiple services uafhængigt
3. **Eventual Consistency:** Services opdateres eventual, ikke immediate
4. **Database per Service:** Hver service har sin egen database
5. **Event-Driven Communication:** Services kommunikerer primært gennem RabbitMQ events

**Fremtidige forbedringer:**
- Implementer Outbox Pattern for garanteret event delivery
- Tilføj compensation handlers for SAGA rollback
- Implementer stock reservation før betaling
- Tilføj retry logic og dead letter queues

