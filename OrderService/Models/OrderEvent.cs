namespace OrderService.Models;

public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemEvent> OrderItems { get; set; } = new List<OrderItemEvent>();
}

public class OrderItemEvent
{
    public Guid OrderItemId { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderPaidEvent
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime PaidDate { get; set; }
}

public class OrderShippedEvent
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string BookISBN { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime ShippedDate { get; set; }
}

public class WarehouseStockEvent
{
    public int Id { get; set; }
    public string BookISBN { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
}
