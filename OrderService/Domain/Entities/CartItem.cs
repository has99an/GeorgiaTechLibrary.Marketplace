using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing an item in a shopping cart
/// </summary>
public class CartItem
{
    public Guid CartItemId { get; private set; }
    public Guid ShoppingCartId { get; private set; }
    public string BookISBN { get; private set; }
    public string SellerId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public DateTime AddedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }

    // Navigation property
    public ShoppingCart ShoppingCart { get; private set; } = null!;

    // Private constructor for EF Core
    private CartItem()
    {
        BookISBN = string.Empty;
        SellerId = string.Empty;
        UnitPrice = Money.Zero();
    }

    private CartItem(
        Guid cartItemId,
        string bookISBN,
        string sellerId,
        int quantity,
        Money unitPrice)
    {
        CartItemId = cartItemId;
        BookISBN = bookISBN;
        SellerId = sellerId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        AddedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new cart item
    /// </summary>
    public static CartItem Create(
        string bookISBN,
        string sellerId,
        int quantity,
        decimal unitPrice)
    {
        ValidateBookISBN(bookISBN);
        ValidateSellerId(sellerId);
        ValidateQuantity(quantity);
        ValidateUnitPrice(unitPrice);

        return new CartItem(
            Guid.NewGuid(),
            bookISBN,
            sellerId,
            quantity,
            Money.Create(unitPrice));
    }

    /// <summary>
    /// Updates the quantity
    /// </summary>
    public void UpdateQuantity(int newQuantity)
    {
        ValidateQuantity(newQuantity);
        Quantity = newQuantity;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the price (for price changes)
    /// </summary>
    public void UpdatePrice(decimal newPrice)
    {
        ValidateUnitPrice(newPrice);
        UnitPrice = Money.Create(newPrice);
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the total price for this cart item
    /// </summary>
    public Money CalculateTotal()
    {
        return UnitPrice.Multiply(Quantity);
    }

    /// <summary>
    /// Converts this cart item to an order item
    /// </summary>
    public OrderItem ToOrderItem()
    {
        return OrderItem.Create(BookISBN, SellerId, Quantity, UnitPrice.Amount);
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

