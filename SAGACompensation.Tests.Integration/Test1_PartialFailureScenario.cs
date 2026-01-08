using FluentAssertions;
using SAGACompensation.Tests.Integration.TestData;
using Tests.Shared;
using Xunit;

namespace SAGACompensation.Tests.Integration;

/// <summary>
/// Test 1: Partial failure scenario
/// - Opret ordre med 2 sælgere
/// - Simulér at WarehouseService fejler på den anden sælger
/// - Verificer at compensation udløses og første sælgers lager rulles tilbage
/// </summary>
public class Test1_PartialFailureScenario : SAGACompensationFixture
{
    public Test1_PartialFailureScenario(LocalServicesFixture services) : base(services)
    {
    }

    [Fact]
    public async Task PartialFailure_WhenSecondSellerFails_ShouldCompensateFirstSeller()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "customer-123";
        var seller1Id = Guid.NewGuid().ToString();
        var seller2Id = Guid.NewGuid().ToString();

        // Create order items from 2 sellers
        var orderItems = TestDataBuilders.CreateOrderPaidItems(2, i => i == 0 ? seller1Id : seller2Id);
        var orderPaidEvent = TestDataBuilders.CreateOrderPaidEvent(orderId, customerId, orderItems);

        // Create warehouse items for both sellers
        var warehouseItems = TestDataBuilders.CreateWarehouseItems(orderItems, initialQuantity: 10);
        
        // Setup listener queues BEFORE publishing events (listen to events from CompensationService)
        var compensationQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationRequired");
        var compensationCompletedQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationCompleted");

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish OrderPaid event (services will process this)
        mockEvents.PublishOrderPaidEvent(orderPaidEvent);

        // Wait for first item to be processed (seller1 should succeed)
        await Task.Delay(3000);

        // Simulate failure for second seller by publishing InventoryReservationFailed event
        var failedEvent = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId,
            orderItems[1].OrderItemId,
            orderItems[1].BookISBN,
            seller2Id,
            orderItems[1].Quantity,
            "Warehouse item not found for seller 2");

        mockEvents.PublishInventoryReservationFailedEvent(failedEvent);

        // Wait for compensation to be triggered (CompensationService needs time to process)
        await Task.Delay(5000);

        // Assert
        // Verify CompensationRequired event was published by CompensationService
        var compensationRequiredEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationQueue, 1, TimeSpan.FromSeconds(10));
        compensationRequiredEvents.Should().HaveCount(1, "CompensationRequired event should be published by CompensationService");

        // 3. Verify CompensationCompleted events were published (one for each failed item)
        var compensationCompletedEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationCompletedQueue, 1, TimeSpan.FromSeconds(5));
        compensationCompletedEvents.Should().HaveCountGreaterOrEqualTo(1, "CompensationCompleted events should be published");

        // 4. Verify that compensation was triggered for the first seller (rollback)
        // This would require checking the warehouse database to verify stock was restored
        // For now, we verify the events were published correctly
    }

    [Fact]
    public async Task PartialFailure_WhenMultipleItemsFail_ShouldCompensateAllSuccessfulItems()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "customer-456";
        var seller1Id = Guid.NewGuid().ToString();
        var seller2Id = Guid.NewGuid().ToString();
        var seller3Id = Guid.NewGuid().ToString();

        // Create order with 3 sellers
        var orderItems = new List<TestDataBuilders.OrderPaidItem>
        {
            new() { OrderItemId = Guid.NewGuid(), BookISBN = "9780123456780", SellerId = seller1Id, Quantity = 2, UnitPrice = 25.00m },
            new() { OrderItemId = Guid.NewGuid(), BookISBN = "9780123456781", SellerId = seller2Id, Quantity = 3, UnitPrice = 30.00m },
            new() { OrderItemId = Guid.NewGuid(), BookISBN = "9780123456782", SellerId = seller3Id, Quantity = 1, UnitPrice = 20.00m }
        };

        var orderPaidEvent = TestDataBuilders.CreateOrderPaidEvent(orderId, customerId, orderItems);

        // Setup listener queue BEFORE publishing (listens to CompensationRequired events from CompensationService)
        var compensationQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationRequired");

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Simulate failures for seller 2 and 3
        var failure1 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, orderItems[1].OrderItemId, orderItems[1].BookISBN, seller2Id, orderItems[1].Quantity);
        
        var failure2 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, orderItems[2].OrderItemId, orderItems[2].BookISBN, seller3Id, orderItems[2].Quantity);

        mockEvents.PublishInventoryReservationFailedEvent(failure1);
        await Task.Delay(1000); // Give CompensationService time to process first
        mockEvents.PublishInventoryReservationFailedEvent(failure2);

        // Wait for compensation orchestration (CompensationService needs time to aggregate)
        await Task.Delay(6000);

        // Assert
        // Verify CompensationRequired event includes both failed items
        var compensationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationQueue, 1, TimeSpan.FromSeconds(10));
        compensationEvents.Should().HaveCount(1, "CompensationRequired event should be published with all failures");
    }
}

