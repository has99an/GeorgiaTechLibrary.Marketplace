using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing an order aggregate root
/// </summary>
public class Order
{
    public Guid OrderId { get; private set; }
    public string CustomerId { get; private set; }
    public DateTime OrderDate { get; private set; }
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime? PaidDate { get; private set; }
    public DateTime? ShippedDate { get; private set; }
    public DateTime? DeliveredDate { get; private set; }
    public DateTime? CancelledDate { get; private set; }
    public DateTime? RefundedDate { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? RefundReason { get; private set; }

    private readonly List<OrderItem> _orderItems = new();
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

    // Private constructor for EF Core
    private Order()
    {
        CustomerId = string.Empty;
        TotalAmount = Money.Zero();
        Status = OrderStatus.Pending;
    }

    private Order(
        Guid orderId,
        string customerId,
        DateTime orderDate,
        List<OrderItem> orderItems)
    {
        OrderId = orderId;
        CustomerId = customerId;
        OrderDate = orderDate;
        Status = OrderStatus.Pending;
        _orderItems = orderItems;
        TotalAmount = CalculateTotalAmount();
    }

    /// <summary>
    /// Factory method to create a new order
    /// </summary>
    public static Order Create(string customerId, List<OrderItem> orderItems)
    {
        ValidateCustomerId(customerId);
        ValidateOrderItems(orderItems);

        return new Order(
            Guid.NewGuid(),
            customerId,
            DateTime.UtcNow,
            orderItems);
    }

    /// <summary>
    /// Processes payment for the order
    /// </summary>
    public void ProcessPayment(decimal paymentAmount)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOrderStateException(
                Status.ToString(), 
                OrderStatus.Paid.ToString());

        if (paymentAmount != TotalAmount.Amount)
            throw new InvalidPaymentException(TotalAmount.Amount, paymentAmount);

        Status = OrderStatus.Paid;
        PaidDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the order as shipped
    /// </summary>
    public void MarkAsShipped()
    {
        if (!Status.CanTransitionTo(OrderStatus.Shipped))
            throw new InvalidOrderStateException(
                Status.ToString(), 
                OrderStatus.Shipped.ToString());

        Status = OrderStatus.Shipped;
        ShippedDate = DateTime.UtcNow;

        // Mark all items as shipped
        foreach (var item in _orderItems)
        {
            item.MarkAsShipped();
        }
    }

    /// <summary>
    /// Marks the order as delivered
    /// </summary>
    public void MarkAsDelivered()
    {
        if (!Status.CanTransitionTo(OrderStatus.Delivered))
            throw new InvalidOrderStateException(
                Status.ToString(), 
                OrderStatus.Delivered.ToString());

        Status = OrderStatus.Delivered;
        DeliveredDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the order
    /// </summary>
    public void Cancel(string reason)
    {
        if (!Status.CanBeCancelled())
            throw new InvalidOrderStateException(
                $"Order in status '{Status}' cannot be cancelled");

        Status = OrderStatus.Cancelled;
        CancelledDate = DateTime.UtcNow;
        CancellationReason = reason;
    }

    /// <summary>
    /// Processes a refund for the order
    /// </summary>
    public void ProcessRefund(string reason)
    {
        if (!Status.CanBeRefunded())
            throw new InvalidOrderStateException(
                $"Order in status '{Status}' cannot be refunded");

        Status = OrderStatus.Refunded;
        RefundedDate = DateTime.UtcNow;
        RefundReason = reason;
    }

    /// <summary>
    /// Checks if the order can be modified
    /// </summary>
    public bool CanBeModified()
    {
        return Status == OrderStatus.Pending;
    }

    /// <summary>
    /// Gets all unique seller IDs in this order
    /// </summary>
    public IEnumerable<string> GetSellerIds()
    {
        return _orderItems.Select(item => item.SellerId).Distinct();
    }

    /// <summary>
    /// Gets items for a specific seller
    /// </summary>
    public IEnumerable<OrderItem> GetItemsForSeller(string sellerId)
    {
        return _orderItems.Where(item => item.SellerId == sellerId);
    }

    private Money CalculateTotalAmount()
    {
        if (!_orderItems.Any())
            return Money.Zero();

        var total = Money.Zero();
        foreach (var item in _orderItems)
        {
            total = total.Add(item.CalculateTotal());
        }
        return total;
    }

    private static void ValidateCustomerId(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));

        if (customerId.Length > 100)
            throw new ArgumentException("Customer ID cannot exceed 100 characters", nameof(customerId));
    }

    private static void ValidateOrderItems(List<OrderItem> orderItems)
    {
        if (orderItems == null || !orderItems.Any())
            throw new ArgumentException("Order must contain at least one item", nameof(orderItems));

        if (orderItems.Count > 100)
            throw new ArgumentException("Order cannot contain more than 100 items", nameof(orderItems));
    }
}

