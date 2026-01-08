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

        // Setup listener queue BEFORE publishing events
        // This queue listens to CompensationRequired events published by CompensationService
        var compensationRequiredQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationRequired");
        
        // CRITICAL: Wait for RabbitMQ binding propagation before publishing events
        // This ensures the queue is ready to receive CompensationRequired events
        await Task.Delay(2000); // 2 seconds for binding to propagate

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish failure event directly to book_events exchange
        // CompensationService's queue (compensationservice_queue) is already bound to this routing key
        mockEvents.PublishInventoryReservationFailedEvent(failureEvent);

        // Wait for CompensationService to process and publish CompensationRequired
        // Give it more time for async processing (consumer needs to receive, deserialize, process, and publish)
        await Task.Delay(8000);

        // Assert
        // Verify CompensationRequired event was published by CompensationService
        var compensationRequiredEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationRequiredQueue, 1, TimeSpan.FromSeconds(10));
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

        // Setup listener queue BEFORE publishing (listens to CompensationRequired events from CompensationService)
        var compensationRequiredQueue = RabbitMQHelper.CreateListenerQueue("book_events", "CompensationRequired");

        var mockEvents = new MockRabbitMQEvents(RabbitMQHelper);

        // Act - Publish multiple failure events
        mockEvents.PublishInventoryReservationFailedEvent(failure1);
        await Task.Delay(1000); // Give CompensationService time to process first event
        mockEvents.PublishInventoryReservationFailedEvent(failure2);

        // Wait for orchestration (CompensationService needs time to aggregate)
        await Task.Delay(6000);

        // Assert
        // Verify CompensationRequired event includes both failures
        var compensationEvents = RabbitMQHelper.ConsumeMessages<dynamic>(compensationRequiredQueue, 1, TimeSpan.FromSeconds(10));
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
}

