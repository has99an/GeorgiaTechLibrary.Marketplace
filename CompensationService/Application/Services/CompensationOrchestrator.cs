using CompensationService.Models;
using CompensationService.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace CompensationService.Application.Services;

/// <summary>
/// Orchestrates compensation flow for failed order items
/// </summary>
public class CompensationOrchestrator
{
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<CompensationOrchestrator> _logger;
    private readonly Dictionary<Guid, OrderCompensationState> _orderStates = new();

    public CompensationOrchestrator(
        IMessageProducer messageProducer,
        ILogger<CompensationOrchestrator> logger)
    {
        _messageProducer = messageProducer;
        _logger = logger;
    }

    /// <summary>
    /// Handles inventory reservation failure
    /// </summary>
    public void HandleInventoryReservationFailed(InventoryReservationFailedEvent failedEvent)
    {
        _logger.LogInformation("Handling InventoryReservationFailed - OrderId: {OrderId}, OrderItemId: {OrderItemId}",
            failedEvent.OrderId, failedEvent.OrderItemId);

        var orderState = GetOrCreateOrderState(failedEvent.OrderId);
        orderState.AddFailedItem(failedEvent.OrderItemId, "InventoryReservation", failedEvent.ErrorMessage);

        EvaluateAndTriggerCompensation(failedEvent.OrderId, orderState);
    }

    /// <summary>
    /// Handles seller stats update failure
    /// </summary>
    public void HandleSellerStatsUpdateFailed(SellerStatsUpdateFailedEvent failedEvent)
    {
        _logger.LogInformation("Handling SellerStatsUpdateFailed - OrderId: {OrderId}, OrderItemId: {OrderItemId}",
            failedEvent.OrderId, failedEvent.OrderItemId);

        var orderState = GetOrCreateOrderState(failedEvent.OrderId);
        orderState.AddFailedItem(failedEvent.OrderItemId, "SellerStatsUpdate", failedEvent.ErrorMessage);

        EvaluateAndTriggerCompensation(failedEvent.OrderId, orderState);
    }

    /// <summary>
    /// Handles notification failure (less critical, but tracked)
    /// </summary>
    public void HandleNotificationFailed(NotificationFailedEvent failedEvent)
    {
        _logger.LogInformation("Handling NotificationFailed - OrderId: {OrderId}, SellerId: {SellerId}",
            failedEvent.OrderId, failedEvent.SellerId);

        // Notifications are less critical - log but don't trigger compensation
        // Only trigger if other critical failures exist
        var orderState = GetOrCreateOrderState(failedEvent.OrderId);
        orderState.AddFailedItem(Guid.Empty, "Notification", failedEvent.ErrorMessage);

        // Only evaluate if there are critical failures
        if (orderState.HasCriticalFailures())
        {
            EvaluateAndTriggerCompensation(failedEvent.OrderId, orderState);
        }
    }

    /// <summary>
    /// Evaluates if compensation is needed and triggers it
    /// </summary>
    private void EvaluateAndTriggerCompensation(Guid orderId, OrderCompensationState orderState)
    {
        // Only trigger compensation if there are critical failures (not just notifications)
        if (!orderState.HasCriticalFailures())
        {
            _logger.LogInformation("No critical failures for OrderId: {OrderId}, skipping compensation", orderId);
            return;
        }

        // Check if compensation was already triggered
        if (orderState.CompensationTriggered)
        {
            _logger.LogInformation("Compensation already triggered for OrderId: {OrderId}", orderId);
            return;
        }

        _logger.LogWarning("Triggering compensation for OrderId: {OrderId} with {Count} failed items",
            orderId, orderState.FailedItems.Count);

        orderState.CompensationTriggered = true;

        // Publish CompensationRequiredEvent
        var compensationEvent = new CompensationRequiredEvent
        {
            OrderId = orderId,
            FailedItems = orderState.FailedItems
                .Where(fi => fi.FailureType != "Notification") // Exclude notification failures from compensation
                .Select(fi => new FailedItem
                {
                    OrderItemId = fi.OrderItemId,
                    FailureType = fi.FailureType,
                    ErrorMessage = fi.ErrorMessage
                })
                .ToList(),
            RequestedAt = DateTime.UtcNow,
            Reason = $"Multiple failures detected: {string.Join(", ", orderState.FailedItems.Select(fi => fi.FailureType).Distinct())}"
        };

        _messageProducer.SendMessage(compensationEvent, "CompensationRequired");
        _logger.LogInformation("Published CompensationRequired event for OrderId: {OrderId}", orderId);
    }

    private OrderCompensationState GetOrCreateOrderState(Guid orderId)
    {
        if (!_orderStates.TryGetValue(orderId, out var state))
        {
            state = new OrderCompensationState { OrderId = orderId };
            _orderStates[orderId] = state;
        }
        return state;
    }

    private class OrderCompensationState
    {
        public Guid OrderId { get; set; }
        public List<FailedItemInfo> FailedItems { get; set; } = new();
        public bool CompensationTriggered { get; set; }

        public void AddFailedItem(Guid orderItemId, string failureType, string errorMessage)
        {
            FailedItems.Add(new FailedItemInfo
            {
                OrderItemId = orderItemId,
                FailureType = failureType,
                ErrorMessage = errorMessage,
                FailedAt = DateTime.UtcNow
            });
        }

        public bool HasCriticalFailures()
        {
            return FailedItems.Any(fi => fi.FailureType != "Notification");
        }
    }

    private class FailedItemInfo
    {
        public Guid OrderItemId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
    }
}

