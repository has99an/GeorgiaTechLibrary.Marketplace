using UserService.Domain.Exceptions;

namespace UserService.Domain.Entities;

/// <summary>
/// Domain entity representing a book listing by a seller
/// </summary>
public class SellerBookListing
{
    public Guid ListingId { get; private set; }
    public Guid SellerId { get; private set; } // FK til SellerProfile
    public string BookISBN { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Quantity { get; private set; }
    public string Condition { get; private set; } = string.Empty; // "New", "Used - Like New", etc.
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSold { get; private set; }
    public DateTime? SoldDate { get; private set; }

    // Navigation property
    public SellerProfile SellerProfile { get; private set; } = null!;

    // Private constructor for EF Core
    private SellerBookListing()
    {
    }

    private SellerBookListing(Guid listingId, Guid sellerId, string bookISBN, decimal price, int quantity, string condition, DateTime createdDate)
    {
        ListingId = listingId;
        SellerId = sellerId;
        BookISBN = bookISBN;
        Price = price;
        Quantity = quantity;
        Condition = condition;
        CreatedDate = createdDate;
        IsActive = true;
        IsSold = false;
        SoldDate = null;
    }

    /// <summary>
    /// Factory method to create a new SellerBookListing with validation
    /// </summary>
    public static SellerBookListing Create(Guid sellerId, string bookISBN, decimal price, int quantity, string condition)
    {
        ValidateBookISBN(bookISBN);
        ValidatePrice(price);
        ValidateQuantity(quantity);
        ValidateCondition(condition);

        return new SellerBookListing(
            Guid.NewGuid(),
            sellerId,
            bookISBN.Trim(),
            price,
            quantity,
            condition.Trim(),
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Updates the listing price
    /// </summary>
    public void UpdatePrice(decimal newPrice)
    {
        if (IsSold)
        {
            throw new ValidationException("Listing", "Cannot update price of a sold listing");
        }
        ValidatePrice(newPrice);
        Price = newPrice;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the listing quantity
    /// </summary>
    public void UpdateQuantity(int newQuantity)
    {
        if (IsSold)
        {
            throw new ValidationException("Listing", "Cannot update quantity of a sold listing");
        }
        ValidateQuantity(newQuantity);
        Quantity = newQuantity;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the listing condition
    /// </summary>
    public void UpdateCondition(string newCondition)
    {
        if (IsSold)
        {
            throw new ValidationException("Listing", "Cannot update condition of a sold listing");
        }
        ValidateCondition(newCondition);
        Condition = newCondition.Trim();
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Decreases quantity when book is sold
    /// </summary>
    public void DecreaseQuantity(int amount)
    {
        if (amount < 0)
        {
            throw new ValidationException("Amount", "Amount cannot be negative");
        }

        if (amount > Quantity)
        {
            throw new ValidationException("Quantity", "Cannot decrease quantity by more than available");
        }

        Quantity -= amount;
        UpdatedDate = DateTime.UtcNow;

        if (Quantity == 0)
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Increases quantity (used for compensation/rollback)
    /// </summary>
    public void IncreaseQuantity(int amount)
    {
        if (amount < 0)
        {
            throw new ValidationException("Amount", "Amount cannot be negative");
        }

        Quantity += amount;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the listing as sold
    /// </summary>
    public void MarkAsSold()
    {
        if (IsSold)
        {
            throw new ValidationException("Listing", "Listing is already marked as sold");
        }
        IsSold = true;
        SoldDate = DateTime.UtcNow;
        IsActive = false;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Unmarks the listing as sold (used for compensation/rollback)
    /// </summary>
    public void UnmarkAsSold()
    {
        if (!IsSold)
        {
            throw new ValidationException("Listing", "Listing is not marked as sold");
        }
        IsSold = false;
        SoldDate = null;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the listing
    /// </summary>
    public void Deactivate()
    {
        if (IsSold)
        {
            throw new ValidationException("Listing", "Cannot deactivate a sold listing");
        }
        IsActive = false;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the listing
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedDate = DateTime.UtcNow;
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
    /// Validates the quantity
    /// </summary>
    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 0)
        {
            throw new ValidationException("Quantity", "Quantity cannot be negative");
        }

        if (quantity > 1000)
        {
            throw new ValidationException("Quantity", "Quantity cannot exceed 1,000");
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

