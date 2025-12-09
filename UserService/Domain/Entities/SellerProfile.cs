using UserService.Domain.Exceptions;

namespace UserService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a Seller Profile with business logic
/// </summary>
public class SellerProfile
{
    public Guid SellerId { get; private set; } // FK til User.UserId
    public decimal Rating { get; private set; } // 0.0 - 5.0
    public int TotalSales { get; private set; } // Antal ordrer
    public int TotalBooksSold { get; private set; } // Antal bøger solgt
    public string? Location { get; private set; } // By/region
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }
    
    // Navigation property
    public User User { get; private set; } = null!;

    // Private constructor for EF Core
    private SellerProfile()
    {
    }

    private SellerProfile(Guid sellerId, decimal rating, int totalSales, int totalBooksSold, string? location, DateTime createdDate)
    {
        SellerId = sellerId;
        Rating = rating;
        TotalSales = totalSales;
        TotalBooksSold = totalBooksSold;
        Location = location;
        CreatedDate = createdDate;
    }

    /// <summary>
    /// Factory method to create a new SellerProfile with validation
    /// </summary>
    public static SellerProfile Create(Guid sellerId, string? location = null)
    {
        ValidateLocation(location);

        return new SellerProfile(
            sellerId,
            rating: 0.0m,
            totalSales: 0,
            totalBooksSold: 0,
            location: string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Updates the seller's location
    /// </summary>
    public void UpdateLocation(string? location)
    {
        ValidateLocation(location);
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the seller's rating (called when order feedback is received)
    /// </summary>
    public void UpdateRating(decimal newRating)
    {
        if (newRating < 0 || newRating > 5)
        {
            throw new ValidationException("Rating", "Rating must be between 0.0 and 5.0");
        }

        Rating = newRating;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments total sales count (called when order is completed)
    /// </summary>
    public void IncrementTotalSales()
    {
        TotalSales++;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds books sold count (called when order is completed)
    /// </summary>
    public void AddBooksSold(int quantity)
    {
        if (quantity < 0)
        {
            throw new ValidationException("Quantity", "Quantity cannot be negative");
        }

        TotalBooksSold += quantity;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates seller statistics from order completion
    /// </summary>
    public void UpdateFromOrder(int booksSold, decimal? orderRating = null)
    {
        IncrementTotalSales();
        AddBooksSold(booksSold);

        if (orderRating.HasValue)
        {
            // Calculate new average rating
            // Simple moving average: (currentRating * totalSales + newRating) / (totalSales + 1)
            var newAverageRating = ((Rating * (TotalSales - 1)) + orderRating.Value) / TotalSales;
            UpdateRating(newAverageRating);
        }
    }

    /// <summary>
    /// Validates the location field
    /// </summary>
    private static void ValidateLocation(string? location)
    {
        if (location != null && location.Trim().Length > 100)
        {
            throw new ValidationException("Location", "Location cannot exceed 100 characters");
        }
    }
}

