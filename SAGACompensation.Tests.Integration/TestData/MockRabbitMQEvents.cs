using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Tests.Shared;

namespace SAGACompensation.Tests.Integration.TestData;

/// <summary>
/// Helper for creating and publishing mock RabbitMQ events
/// </summary>
public class MockRabbitMQEvents
{
    private readonly RabbitMQTestHelper _rabbitMQHelper;

    public MockRabbitMQEvents(RabbitMQTestHelper rabbitMQHelper)
    {
        _rabbitMQHelper = rabbitMQHelper;
    }

    /// <summary>
    /// Publishes OrderPaid event
    /// </summary>
    public void PublishOrderPaidEvent(object orderPaidEvent)
    {
        _rabbitMQHelper.PublishMessage(orderPaidEvent, "book_events", "OrderPaid");
    }

    /// <summary>
    /// Publishes InventoryReservationFailed event
    /// </summary>
    public void PublishInventoryReservationFailedEvent(object failedEvent)
    {
        _rabbitMQHelper.PublishMessage(failedEvent, "book_events", "InventoryReservationFailed");
    }

    /// <summary>
    /// Publishes SellerStatsUpdateFailed event
    /// </summary>
    public void PublishSellerStatsUpdateFailedEvent(object failedEvent)
    {
        _rabbitMQHelper.PublishMessage(failedEvent, "book_events", "SellerStatsUpdateFailed");
    }

    /// <summary>
    /// Publishes CompensationRequired event
    /// </summary>
    public void PublishCompensationRequiredEvent(object compensationEvent)
    {
        _rabbitMQHelper.PublishMessage(compensationEvent, "book_events", "CompensationRequired");
    }

    /// <summary>
    /// Consumes CompensationRequired events
    /// </summary>
    public List<T> ConsumeCompensationRequiredEvents<T>(string queueName, int expectedCount, TimeSpan timeout)
    {
        return _rabbitMQHelper.ConsumeMessages<T>(queueName, expectedCount, timeout);
    }

    /// <summary>
    /// Consumes CompensationCompleted events
    /// </summary>
    public List<T> ConsumeCompensationCompletedEvents<T>(string queueName, int expectedCount, TimeSpan timeout)
    {
        return _rabbitMQHelper.ConsumeMessages<T>(queueName, expectedCount, timeout);
    }

    /// <summary>
    /// Publishes a generic message
    /// </summary>
    public void PublishMessage<T>(T message, string exchangeName, string routingKey)
    {
        _rabbitMQHelper.PublishMessage(message, exchangeName, routingKey);
    }
}

