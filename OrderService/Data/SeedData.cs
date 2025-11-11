using OrderService.Models;

namespace OrderService.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        // Check if data already exists
        if (context.Orders.Any())
        {
            return; // DB has been seeded
        }

        // Seed sample orders
        var orders = new List<Order>
        {
            new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "customer1",
                OrderDate = DateTime.UtcNow.AddDays(-5),
                TotalAmount = 45.98m,
                Status = "Completed",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        BookISBN = "1234567890123",
                        SellerId = "seller1",
                        Quantity = 1,
                        UnitPrice = 25.99m,
                        Status = "Shipped"
                    },
                    new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        BookISBN = "9876543210987",
                        SellerId = "seller2",
                        Quantity = 1,
                        UnitPrice = 19.99m,
                        Status = "Shipped"
                    }
                }
            },
            new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "customer2",
                OrderDate = DateTime.UtcNow.AddDays(-2),
                TotalAmount = 32.50m,
                Status = "Paid",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        BookISBN = "5556667778889",
                        SellerId = "seller1",
                        Quantity = 1,
                        UnitPrice = 32.50m,
                        Status = "Pending"
                    }
                }
            }
        };

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();
    }
}
