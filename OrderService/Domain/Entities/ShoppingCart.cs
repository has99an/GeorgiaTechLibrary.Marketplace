using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a shopping cart aggregate root
/// </summary>
public class ShoppingCart
{
    public Guid ShoppingCartId { get; private set; }
    public string CustomerId { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }

    private readonly List<CartItem> _items = new();
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    // Private constructor for EF Core
    private ShoppingCart()
    {
        CustomerId = string.Empty;
    }

    private ShoppingCart(Guid shoppingCartId, string customerId)
    {
        ShoppingCartId = shoppingCartId;
        CustomerId = customerId;
        CreatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new shopping cart
    /// </summary>
    public static ShoppingCart Create(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));

        return new ShoppingCart(Guid.NewGuid(), customerId);
    }

    /// <summary>
    /// Adds an item to the cart or updates quantity if it already exists
    /// </summary>
    public void AddItem(string bookISBN, string sellerId, int quantity, decimal unitPrice)
    {
        var existingItem = _items.FirstOrDefault(i => 
            i.BookISBN == bookISBN && i.SellerId == sellerId);

        if (existingItem != null)
        {
            // Update quantity of existing item
            existingItem.UpdateQuantity(existingItem.Quantity + quantity);
        }
        else
        {
            // Add new item
            var newItem = CartItem.Create(bookISBN, sellerId, quantity, unitPrice);
            _items.Add(newItem);
        }

        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the quantity of a specific item
    /// </summary>
    public void UpdateItemQuantity(Guid cartItemId, int newQuantity)
    {
        var item = _items.FirstOrDefault(i => i.CartItemId == cartItemId);
        if (item == null)
            throw new ShoppingCartException($"Cart item with ID '{cartItemId}' not found");

        item.UpdateQuantity(newQuantity);
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes an item from the cart
    /// </summary>
    public void RemoveItem(Guid cartItemId)
    {
        var item = _items.FirstOrDefault(i => i.CartItemId == cartItemId);
        if (item == null)
            throw new ShoppingCartException($"Cart item with ID '{cartItemId}' not found");

        _items.Remove(item);
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears all items from the cart
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the total amount for all items in the cart
    /// </summary>
    public Money CalculateTotal()
    {
        if (!_items.Any())
            return Money.Zero();

        var total = Money.Zero();
        foreach (var item in _items)
        {
            total = total.Add(item.CalculateTotal());
        }
        return total;
    }

    /// <summary>
    /// Gets the total number of items in the cart
    /// </summary>
    public int GetItemCount()
    {
        return _items.Sum(i => i.Quantity);
    }

    /// <summary>
    /// Checks if the cart is empty
    /// </summary>
    public bool IsEmpty()
    {
        return !_items.Any();
    }

    /// <summary>
    /// Converts the cart to an order
    /// </summary>
    public Order ConvertToOrder()
    {
        if (IsEmpty())
            throw new ShoppingCartException("Cannot create order from empty cart");

        var orderItems = _items.Select(item => item.ToOrderItem()).ToList();
        var order = Order.Create(CustomerId, orderItems);

        // Clear the cart after conversion
        Clear();

        return order;
    }

    /// <summary>
    /// Checks if a specific item exists in the cart
    /// </summary>
    public bool HasItem(string bookISBN, string sellerId)
    {
        return _items.Any(i => i.BookISBN == bookISBN && i.SellerId == sellerId);
    }

    /// <summary>
    /// Gets all unique seller IDs in the cart
    /// </summary>
    public IEnumerable<string> GetSellerIds()
    {
        return _items.Select(item => item.SellerId).Distinct();
    }
}

