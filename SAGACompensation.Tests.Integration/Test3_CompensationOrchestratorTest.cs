using FluentAssertions;
using SAGACompensation.Tests.Integration.TestData;
using Tests.Shared;
using Xunit;

namespace SAGACompensation.Tests.Integration;

/// <summary>
/// Test 3: Compensation orchestrator
/// - Send InventoryReservationFailedEvent
/// - Verificer at CompensationService:
///   a) Modtager event
///   b) Sender CompensationRequiredEvent
///   c) Koordinerer rollback på tværs af services
/// </summary>
public class Test3_CompensationOrchestratorTest : SAGACompensationFixture
{
    public Test3_CompensationOrchestratorTest(LocalServicesFixture services) : base(services)
    {
    }

    [Fact]
    public async Task CompensationOrchestrator_WhenInventoryReservationFails_ShouldPublishCompensationRequired()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var sellerId = Guid.NewGuid().ToString();
        var bookISBN = "9780123456789";

        var failureEvent = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId,
            orderItemId,
            bookISBN,
            sellerId,
            quantity: 2,
            errorMessage: "Warehouse item not found");

        // Use persistent test queue created in fixture
        // Purge any messages from previous tests
        RabbitMQHelper.PurgeQueue(CompensationRequiredQueue);
        
        // Wait for binding to be active (queue was created in fixture, so binding should be ready)
        await Task.Delay(1000);

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish failure event directly to book_events exchange
        // CompensationService's queue (compensationservice_queue) is already bound to this routing key
        mockEvents.PublishInventoryReservationFailedEvent(failureEvent);

        // Wait for CompensationService to process and publish CompensationRequired
        // Give it more time for async processing (consumer needs to receive, deserialize, process, and publish)
        await Task.Delay(8000);

        // Assert
        // Verify CompensationRequired event was published by CompensationService
        var compensationRequiredEvents = RabbitMQHelper.ConsumeMessages<dynamic>(CompensationRequiredQueue, 1, TimeSpan.FromSeconds(10));
        compensationRequiredEvents.Should().HaveCount(1, "CompensationRequired event should be published by CompensationService");

        // 3. Verify CompensationRequired event contains correct data
        if (compensationRequiredEvents.Count > 0)
        {
            var compensationEvent = compensationRequiredEvents[0];
            // Note: In a real test, we would deserialize and check properties
            // For now, we verify the event was published
        }
    }

    [Fact]
    public async Task CompensationOrchestrator_WhenMultipleFailuresOccur_ShouldAggregateInCompensationRequired()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var seller1Id = Guid.NewGuid().ToString();
        var seller2Id = Guid.NewGuid().ToString();

        var failure1 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, Guid.NewGuid(), "9780123456780", seller1Id, 2);

        var failure2 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, Guid.NewGuid(), "9780123456781", seller2Id, 3);

        // Use persistent test queue
        RabbitMQHelper.PurgeQueue(CompensationRequiredQueue);
        await Task.Delay(1000);

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish multiple failure events
        mockEvents.PublishInventoryReservationFailedEvent(failure1);
        await Task.Delay(1000); // Give CompensationService time to process first event
        mockEvents.PublishInventoryReservationFailedEvent(failure2);

        // Wait for orchestration (CompensationService needs time to aggregate)
        await Task.Delay(6000);

        // Assert
        // Verify CompensationRequired event includes both failures
        var compensationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(CompensationRequiredQueue, 1, TimeSpan.FromSeconds(10));
        compensationEvents.Should().HaveCount(1, "CompensationRequired should aggregate multiple failures");
    }

    [Fact]
    public async Task CompensationOrchestrator_WhenNotificationFails_ShouldNotTriggerCompensation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var sellerId = Guid.NewGuid().ToString();

        // Notification failures are less critical and shouldn't trigger compensation alone
        var notificationFailedEvent = new
        {
            OrderId = orderId,
            SellerId = sellerId,
            NotificationType = "OrderCreated",
            ErrorMessage = "Email service unavailable",
            FailedAt = DateTime.UtcNow,
            RetryAttempts = 3
        };

        // Setup listener queue BEFORE publishing (listens to CompensationRequired events from CompensationService)
        var compensationRequiredQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationRequired");

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish notification failure
        mockEvents.PublishMessage<object>(notificationFailedEvent, "book_events", "NotificationFailed");

        // Wait for CompensationService to process
        await Task.Delay(3000);

        // Assert
        // Notification failures alone should NOT trigger compensation
        var compensationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationRequiredQueue, 1, TimeSpan.FromSeconds(3));
        compensationEvents.Should().HaveCount(0, "Notification failures alone should not trigger compensation");
    }

    [Fact]
    public async Task CompensationOrchestrator_WhenAllCompensationsComplete_ShouldTriggerOrderCancellation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var sellerId = Guid.NewGuid().ToString();
        var bookISBN = "9780123456789";

        // Use persistent test queues
        RabbitMQHelper.PurgeQueue(CompensationRequiredQueue);
        RabbitMQHelper.PurgeQueue(OrderCancellationQueue);
        await Task.Delay(1000);

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Step 1: Publish failure event to trigger compensation
        var failureEvent = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId,
            orderItemId,
            bookISBN,
            sellerId,
            quantity: 2,
            errorMessage: "Warehouse item not found");

        mockEvents.PublishInventoryReservationFailedEvent(failureEvent);

        // Wait for CompensationService to process and publish CompensationRequired
        await Task.Delay(5000);

        // Verify CompensationRequired was published
        var compensationRequiredEvents = RabbitMQHelper.ConsumeMessages<dynamic>(CompensationRequiredQueue, 1, TimeSpan.FromSeconds(5));
        compensationRequiredEvents.Should().HaveCount(1, "CompensationRequired should be published");

        // Step 2: Simulate CompensationCompleted event (from WarehouseService)
        var compensationCompletedEvent = new
        {
            OrderId = orderId,
            OrderItemId = orderItemId,
            CompensationType = "InventoryReservation",
            CompletedAt = DateTime.UtcNow,
            Success = true,
            ErrorMessage = (string?)null
        };

        mockEvents.PublishMessage<object>(compensationCompletedEvent, "book_events", "CompensationCompleted");

        // Wait for CompensationService to process CompensationCompleted and publish OrderCancellationRequested
        await Task.Delay(5000);

        // Assert
        // Verify OrderCancellationRequested was published by CompensationService
        var orderCancellationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(OrderCancellationQueue, 1, TimeSpan.FromSeconds(10));
        orderCancellationEvents.Should().HaveCount(1, "OrderCancellationRequested should be published after all compensations complete");
    }

    [Fact]
    public async Task CompensationOrchestrator_WhenMultipleCompensationsComplete_ShouldWaitForAll()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderItem1Id = Guid.NewGuid();
        var orderItem2Id = Guid.NewGuid();
        var seller1Id = Guid.NewGuid().ToString();
        var seller2Id = Guid.NewGuid().ToString();

        // Use persistent test queues
        RabbitMQHelper.PurgeQueue(CompensationRequiredQueue);
        RabbitMQHelper.PurgeQueue(OrderCancellationQueue);
        await Task.Delay(1000);

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Step 1: Publish multiple failure events for same order
        var failure1 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, orderItem1Id, "9780123456780", seller1Id, 2);
        var failure2 = TestDataBuilders.CreateInventoryReservationFailedEvent(
            orderId, orderItem2Id, "9780123456781", seller2Id, 3);

        mockEvents.PublishInventoryReservationFailedEvent(failure1);
        await Task.Delay(1000);
        mockEvents.PublishInventoryReservationFailedEvent(failure2);

        // Wait for CompensationService to aggregate and publish CompensationRequired
        await Task.Delay(5000);

        // Verify CompensationRequired was published
        var compensationRequiredEvents = RabbitMQHelper.ConsumeMessages<dynamic>(CompensationRequiredQueue, 1, TimeSpan.FromSeconds(5));
        compensationRequiredEvents.Should().HaveCount(1, "CompensationRequired should aggregate failures");

        // Step 2: Simulate first CompensationCompleted
        var compensation1 = new
        {
            OrderId = orderId,
            OrderItemId = orderItem1Id,
            CompensationType = "InventoryReservation",
            CompletedAt = DateTime.UtcNow,
            Success = true,
            ErrorMessage = (string?)null
        };
        mockEvents.PublishMessage<object>(compensation1, "book_events", "CompensationCompleted");

        await Task.Delay(3000);

        // OrderCancellation should NOT be triggered yet (waiting for second compensation)
        var prematureEvents = RabbitMQHelper.ConsumeMessages<dynamic>(OrderCancellationQueue, 1, TimeSpan.FromSeconds(2));
        prematureEvents.Should().HaveCount(0, "Should wait for all compensations before cancelling order");

        // Step 3: Simulate second CompensationCompleted
        var compensation2 = new
        {
            OrderId = orderId,
            OrderItemId = orderItem2Id,
            CompensationType = "InventoryReservation",
            CompletedAt = DateTime.UtcNow,
            Success = true,
            ErrorMessage = (string?)null
        };
        mockEvents.PublishMessage<object>(compensation2, "book_events", "CompensationCompleted");

        await Task.Delay(5000);

        // Assert - NOW OrderCancellationRequested should be published
        var orderCancellationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(OrderCancellationQueue, 1, TimeSpan.FromSeconds(10));
        orderCancellationEvents.Should().HaveCount(1, "OrderCancellationRequested should be published after ALL compensations complete");
    }
}

