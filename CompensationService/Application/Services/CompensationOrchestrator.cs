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
    /// Handles compensation completed events and triggers order cancellation when all compensations are done
    /// </summary>
    public void HandleCompensationCompleted(CompensationCompletedEvent completedEvent)
    {
        _logger.LogInformation("Handling CompensationCompleted - OrderId: {OrderId}, OrderItemId: {OrderItemId}, Type: {Type}, Success: {Success}",
            completedEvent.OrderId, completedEvent.OrderItemId, completedEvent.CompensationType, completedEvent.Success);

        // Get the order state (must exist if we got here)
        if (!_orderStates.TryGetValue(completedEvent.OrderId, out var orderState))
        {
            _logger.LogWarning("Received CompensationCompleted for unknown OrderId: {OrderId}", completedEvent.OrderId);
            return;
        }

        // Track this completed compensation
        orderState.AddCompletedCompensation(
            completedEvent.OrderItemId,
            completedEvent.CompensationType,
            completedEvent.Success,
            completedEvent.ErrorMessage);

        _logger.LogInformation("Compensation progress for OrderId {OrderId}: {Completed}/{Expected} completed",
            completedEvent.OrderId, orderState.CompletedCompensations.Count, orderState.ExpectedCompensations);

        // Check if all compensations are complete
        if (!orderState.AreAllCompensationsComplete())
        {
            _logger.LogInformation("Waiting for more compensations - OrderId: {OrderId}", completedEvent.OrderId);
            return;
        }

        // Check if order cancellation was already requested
        if (orderState.OrderCancellationRequested)
        {
            _logger.LogInformation("Order cancellation already requested for OrderId: {OrderId}", completedEvent.OrderId);
            return;
        }

        _logger.LogInformation("All compensations complete for OrderId: {OrderId}", completedEvent.OrderId);

        // Check if all compensations succeeded
        if (!orderState.DidAllCompensationsSucceed())
        {
            _logger.LogWarning("Some compensations failed for OrderId: {OrderId}. Review required.", completedEvent.OrderId);
            // Still proceed with cancellation, but log the failures
            var failedCompensations = orderState.CompletedCompensations.Where(cc => !cc.Success).ToList();
            foreach (var failed in failedCompensations)
            {
                _logger.LogWarning("Failed compensation - OrderItemId: {OrderItemId}, Type: {Type}, Error: {Error}",
                    failed.OrderItemId, failed.CompensationType, failed.ErrorMessage);
            }
        }

        // Mark cancellation as requested
        orderState.OrderCancellationRequested = true;

        // Publish OrderCancellationRequested event
        var cancellationEvent = new OrderCancellationRequestedEvent
        {
            OrderId = completedEvent.OrderId,
            Reason = "Compensation completed for failed order items",
            RequestedAt = DateTime.UtcNow,
            FailedItems = orderState.FailedItems
                .Where(fi => fi.FailureType != "Notification")
                .Select(fi => new FailedItem
                {
                    OrderItemId = fi.OrderItemId,
                    FailureType = fi.FailureType,
                    ErrorMessage = fi.ErrorMessage
                })
                .ToList()
        };

        _messageProducer.SendMessage(cancellationEvent, "OrderCancellationRequested");
        _logger.LogInformation("Published OrderCancellationRequested event for OrderId: {OrderId}", completedEvent.OrderId);
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

        // Count expected compensations (exclude notification failures)
        var criticalFailedItems = orderState.FailedItems
            .Where(fi => fi.FailureType != "Notification")
            .ToList();

        // If compensation was already triggered, just update the expected count
        // This handles cases where new failures arrive after CompensationRequired was published
        if (orderState.CompensationTriggered)
        {
            var oldExpected = orderState.ExpectedCompensations;
            orderState.ExpectedCompensations = criticalFailedItems.Count;
            
            if (oldExpected != orderState.ExpectedCompensations)
            {
                _logger.LogInformation("Updated expected compensations from {Old} to {New} for OrderId: {OrderId}", 
                    oldExpected, orderState.ExpectedCompensations, orderId);
            }
            else
            {
                _logger.LogInformation("Compensation already triggered for OrderId: {OrderId}", orderId);
            }
            return;
        }

        _logger.LogWarning("Triggering compensation for OrderId: {OrderId} with {Count} failed items",
            orderId, orderState.FailedItems.Count);

        orderState.CompensationTriggered = true;
        orderState.ExpectedCompensations = criticalFailedItems.Count;

        _logger.LogInformation("Setting expected compensations to {Count} for OrderId: {OrderId}", 
            orderState.ExpectedCompensations, orderId);

        // Publish CompensationRequiredEvent
        var compensationEvent = new CompensationRequiredEvent
        {
            OrderId = orderId,
            FailedItems = criticalFailedItems
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
        public int ExpectedCompensations { get; set; }
        public List<CompensationCompletedInfo> CompletedCompensations { get; set; } = new();
        public bool OrderCancellationRequested { get; set; }

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

        public void AddCompletedCompensation(Guid orderItemId, string compensationType, bool success, string? errorMessage)
        {
            CompletedCompensations.Add(new CompensationCompletedInfo
            {
                OrderItemId = orderItemId,
                CompensationType = compensationType,
                Success = success,
                ErrorMessage = errorMessage,
                CompletedAt = DateTime.UtcNow
            });
        }

        public bool HasCriticalFailures()
        {
            return FailedItems.Any(fi => fi.FailureType != "Notification");
        }

        public bool AreAllCompensationsComplete()
        {
            return ExpectedCompensations > 0 && CompletedCompensations.Count >= ExpectedCompensations;
        }

        public bool DidAllCompensationsSucceed()
        {
            return CompletedCompensations.All(cc => cc.Success);
        }
    }

    private class FailedItemInfo
    {
        public Guid OrderItemId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
    }

    private class CompensationCompletedInfo
    {
        public Guid OrderItemId { get; set; }
        public string CompensationType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}

