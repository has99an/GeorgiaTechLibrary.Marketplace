using OrderService.Domain.Entities;
using OrderService.Domain.ValueObjects;
using WarehouseService.Models;
using UserService.Domain.Entities;

namespace SAGACompensation.Tests.Integration.TestData;

/// <summary>
/// Test data builders for SAGA compensation tests
/// </summary>
public static class TestDataBuilders
{
    /// <summary>
    /// Creates a test order with items from multiple sellers
    /// </summary>
    public static Order CreateMultiSellerOrder(Guid orderId, string customerId, int sellerCount = 2)
    {
        var sellers = new List<string>();
        for (int i = 0; i < sellerCount; i++)
        {
            sellers.Add(Guid.NewGuid().ToString());
        }

        var orderItems = new List<OrderItem>();
        int itemIndex = 0;
        foreach (var sellerId in sellers)
        {
            var orderItem = OrderItem.Create(
                $"978012345678{itemIndex}",
                sellerId,
                quantity: 2,
                unitPrice: 25.00m);
            
            orderItems.Add(orderItem);
            itemIndex++;
        }

        var deliveryAddress = Address.Create("123 Main St", "Atlanta", "30332", "GA", "USA");
        var order = Order.Create(customerId, orderItems, deliveryAddress);
        
        return order;
    }

    /// <summary>
    /// Creates OrderPaid event for testing
    /// </summary>
    public static object CreateOrderPaidEvent(Guid orderId, string customerId, List<OrderPaidItem> items)
    {
        return new
        {
            OrderId = orderId,
            CustomerId = customerId,
            TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice),
            PaidDate = DateTime.UtcNow,
            OrderItems = items
        };
    }

    /// <summary>
    /// Creates OrderPaid items for testing
    /// </summary>
    public static List<OrderPaidItem> CreateOrderPaidItems(int count, Func<int, string>? sellerIdGenerator = null)
    {
        var items = new List<OrderPaidItem>();
        for (int i = 0; i < count; i++)
        {
            items.Add(new OrderPaidItem
            {
                OrderItemId = Guid.NewGuid(),
                BookISBN = $"978012345678{i}",
                SellerId = sellerIdGenerator?.Invoke(i) ?? Guid.NewGuid().ToString(),
                Quantity = 2,
                UnitPrice = 25.00m
            });
        }
        return items;
    }

    /// <summary>
    /// Creates warehouse items for testing
    /// </summary>
    public static List<WarehouseItem> CreateWarehouseItems(List<OrderPaidItem> orderItems, int initialQuantity = 10)
    {
        return orderItems.Select(item => new WarehouseItem
        {
            Id = 0, // Database will generate the ID
            BookISBN = item.BookISBN,
            SellerId = item.SellerId,
            Quantity = initialQuantity,
            Price = item.UnitPrice,
            Location = "Student Seller",
            IsNew = true
        }).ToList();
    }

    /// <summary>
    /// Creates InventoryReservationFailed event
    /// </summary>
    public static object CreateInventoryReservationFailedEvent(
        Guid orderId,
        Guid orderItemId,
        string bookISBN,
        string sellerId,
        int quantity,
        string errorMessage = "Warehouse item not found")
    {
        return new
        {
            OrderId = orderId,
            OrderItemId = orderItemId,
            BookISBN = bookISBN,
            SellerId = sellerId,
            Quantity = quantity,
            ErrorMessage = errorMessage,
            FailedAt = DateTime.UtcNow,
            RetryAttempts = 3
        };
    }

    /// <summary>
    /// Creates SellerStatsUpdateFailed event
    /// </summary>
    public static object CreateSellerStatsUpdateFailedEvent(
        Guid orderId,
        Guid orderItemId,
        string sellerId,
        string bookISBN,
        int quantity,
        string errorMessage = "Listing not found")
    {
        return new
        {
            OrderId = orderId,
            OrderItemId = orderItemId,
            SellerId = sellerId,
            BookISBN = bookISBN,
            Quantity = quantity,
            ErrorMessage = errorMessage,
            FailedAt = DateTime.UtcNow,
            RetryAttempts = 3
        };
    }

    /// <summary>
    /// Creates CompensationRequired event
    /// </summary>
    public static object CreateCompensationRequiredEvent(
        Guid orderId,
        List<FailedItem> failedItems)
    {
        return new
        {
            OrderId = orderId,
            FailedItems = failedItems,
            RequestedAt = DateTime.UtcNow,
            Reason = $"Multiple failures detected: {string.Join(", ", failedItems.Select(fi => fi.FailureType).Distinct())}"
        };
    }

    public class OrderPaidItem
    {
        public Guid OrderItemId { get; set; }
        public string BookISBN { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class FailedItem
    {
        public Guid OrderItemId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

