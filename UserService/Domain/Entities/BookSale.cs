using UserService.Domain.Exceptions;

namespace UserService.Domain.Entities;

/// <summary>
/// Domain entity representing a sale of a book from a seller to a buyer
/// </summary>
public class BookSale
{
    public Guid SaleId { get; private set; }
    public Guid ListingId { get; private set; } // FK til SellerBookListing
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public string BuyerId { get; private set; } = string.Empty; // Køberens ID
    public string BookISBN { get; private set; } = string.Empty;
    public Guid SellerId { get; private set; } // FK til SellerProfile
    public int Quantity { get; private set; } // Antal solgte eksemplarer
    public decimal Price { get; private set; } // Salgspris
    public string Condition { get; private set; } = string.Empty;
    public DateTime SaleDate { get; private set; }
    public DateTime CreatedDate { get; private set; }

    // Navigation properties
    public SellerBookListing Listing { get; private set; } = null!;
    public SellerProfile SellerProfile { get; private set; } = null!;

    // Private constructor for EF Core
    private BookSale()
    {
    }

    private BookSale(
        Guid saleId,
        Guid listingId,
        Guid orderId,
        Guid orderItemId,
        string buyerId,
        string bookISBN,
        Guid sellerId,
        int quantity,
        decimal price,
        string condition,
        DateTime saleDate)
    {
        SaleId = saleId;
        ListingId = listingId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        BuyerId = buyerId;
        BookISBN = bookISBN;
        SellerId = sellerId;
        Quantity = quantity;
        Price = price;
        Condition = condition;
        SaleDate = saleDate;
        CreatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new BookSale with validation
    /// </summary>
    public static BookSale Create(
        Guid listingId,
        Guid orderId,
        Guid orderItemId,
        string buyerId,
        string bookISBN,
        Guid sellerId,
        int quantity,
        decimal price,
        string condition)
    {
        ValidateBuyerId(buyerId);
        ValidateBookISBN(bookISBN);
        ValidateQuantity(quantity);
        ValidatePrice(price);
        ValidateCondition(condition);

        return new BookSale(
            Guid.NewGuid(),
            listingId,
            orderId,
            orderItemId,
            buyerId.Trim(),
            bookISBN.Trim(),
            sellerId,
            quantity,
            price,
            condition.Trim(),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Validates the buyer ID
    /// </summary>
    private static void ValidateBuyerId(string buyerId)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            throw new ValidationException("BuyerId", "Buyer ID is required");
        }
    }

    /// <summary>
    /// Validates the book ISBN (accepts 10 or 13 digits)
    /// </summary>
    private static void ValidateBookISBN(string bookISBN)
    {
        if (string.IsNullOrWhiteSpace(bookISBN))
        {
            throw new ValidationException("BookISBN", "Book ISBN is required");
        }

        var trimmedISBN = bookISBN.Trim();
        
        // ISBN must be either 10 or 13 digits
        if (trimmedISBN.Length != 10 && trimmedISBN.Length != 13)
        {
            throw new ValidationException("BookISBN", "Book ISBN must be 10 or 13 characters");
        }

        if (!trimmedISBN.All(char.IsDigit))
        {
            throw new ValidationException("BookISBN", "Book ISBN must contain only digits");
        }
    }

    /// <summary>
    /// Validates the quantity
    /// </summary>
    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("Quantity", "Quantity must be greater than 0");
        }

        if (quantity > 1000)
        {
            throw new ValidationException("Quantity", "Quantity cannot exceed 1,000");
        }
    }

    /// <summary>
    /// Validates the price
    /// </summary>
    private static void ValidatePrice(decimal price)
    {
        if (price <= 0)
        {
            throw new ValidationException("Price", "Price must be greater than 0");
        }

        if (price > 10000)
        {
            throw new ValidationException("Price", "Price cannot exceed 10,000");
        }
    }

    /// <summary>
    /// Validates the condition
    /// </summary>
    private static void ValidateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ValidationException("Condition", "Condition is required");
        }

        var validConditions = new[] { "New", "Used - Like New", "Used - Good", "Used - Acceptable", "Used" };
        var trimmedCondition = condition.Trim();

        if (!validConditions.Contains(trimmedCondition))
        {
            throw new ValidationException("Condition", $"Condition must be one of: {string.Join(", ", validConditions)}");
        }
    }
}





