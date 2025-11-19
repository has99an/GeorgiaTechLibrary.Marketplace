using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing an item in an order
/// </summary>
public class OrderItem
{
    public Guid OrderItemId { get; private set; }
    public Guid OrderId { get; private set; }
    public string BookISBN { get; private set; }
    public string SellerId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public string Status { get; private set; }

    // Navigation property
    public Order Order { get; private set; } = null!;

    // Private constructor for EF Core
    private OrderItem()
    {
        BookISBN = string.Empty;
        SellerId = string.Empty;
        UnitPrice = Money.Zero();
        Status = "Pending";
    }

    private OrderItem(
        Guid orderItemId,
        string bookISBN,
        string sellerId,
        int quantity,
        Money unitPrice)
    {
        OrderItemId = orderItemId;
        BookISBN = bookISBN;
        SellerId = sellerId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Status = "Pending";
    }

    /// <summary>
    /// Factory method to create a new order item
    /// </summary>
    public static OrderItem Create(
        string bookISBN,
        string sellerId,
        int quantity,
        decimal unitPrice)
    {
        ValidateBookISBN(bookISBN);
        ValidateSellerId(sellerId);
        ValidateQuantity(quantity);
        ValidateUnitPrice(unitPrice);

        return new OrderItem(
            Guid.NewGuid(),
            bookISBN,
            sellerId,
            quantity,
            Money.Create(unitPrice));
    }

    /// <summary>
    /// Calculates the total price for this item
    /// </summary>
    public Money CalculateTotal()
    {
        return UnitPrice.Multiply(Quantity);
    }

    /// <summary>
    /// Updates the quantity
    /// </summary>
    public void UpdateQuantity(int newQuantity)
    {
        ValidateQuantity(newQuantity);
        Quantity = newQuantity;
    }

    /// <summary>
    /// Marks the item as shipped
    /// </summary>
    public void MarkAsShipped()
    {
        if (Status == "Shipped")
            throw new InvalidOperationException("Item is already shipped");

        Status = "Shipped";
    }

    private static void ValidateBookISBN(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("Book ISBN cannot be empty", nameof(isbn));

        if (isbn.Length != 13 && isbn.Length != 10)
            throw new ArgumentException("Book ISBN must be 10 or 13 characters", nameof(isbn));
    }

    private static void ValidateSellerId(string sellerId)
    {
        if (string.IsNullOrWhiteSpace(sellerId))
            throw new ArgumentException("Seller ID cannot be empty", nameof(sellerId));

        if (sellerId.Length > 100)
            throw new ArgumentException("Seller ID cannot exceed 100 characters", nameof(sellerId));
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (quantity > 1000)
            throw new ArgumentException("Quantity cannot exceed 1000", nameof(quantity));
    }

    private static void ValidateUnitPrice(decimal price)
    {
        if (price <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(price));

        if (price > 10000)
            throw new ArgumentException("Unit price cannot exceed $10,000", nameof(price));
    }
}

