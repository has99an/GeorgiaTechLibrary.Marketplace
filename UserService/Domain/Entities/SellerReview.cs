using UserService.Domain.Exceptions;

namespace UserService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a review/rating for a seller from a completed order
/// </summary>
public class SellerReview
{
    public Guid ReviewId { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Rating { get; private set; } // 1.0 - 5.0
    public string? Comment { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }
    
    // Navigation property
    public SellerProfile SellerProfile { get; private set; } = null!;

    // Private constructor for EF Core
    private SellerReview()
    {
    }

    private SellerReview(
        Guid reviewId,
        Guid sellerId,
        Guid orderId,
        Guid customerId,
        decimal rating,
        string? comment,
        DateTime createdDate)
    {
        ReviewId = reviewId;
        SellerId = sellerId;
        OrderId = orderId;
        CustomerId = customerId;
        Rating = rating;
        Comment = comment;
        CreatedDate = createdDate;
    }

    /// <summary>
    /// Factory method to create a new SellerReview with validation
    /// </summary>
    public static SellerReview Create(
        Guid sellerId,
        Guid orderId,
        Guid customerId,
        decimal rating,
        string? comment = null)
    {
        ValidateRating(rating);
        ValidateComment(comment);

        return new SellerReview(
            Guid.NewGuid(),
            sellerId,
            orderId,
            customerId,
            rating,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Updates the review rating and comment
    /// </summary>
    public void UpdateReview(decimal newRating, string? newComment = null)
    {
        ValidateRating(newRating);
        ValidateComment(newComment);

        Rating = newRating;
        Comment = string.IsNullOrWhiteSpace(newComment) ? null : newComment.Trim();
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates the rating value
    /// </summary>
    private static void ValidateRating(decimal rating)
    {
        if (rating < 1.0m || rating > 5.0m)
        {
            throw new ValidationException("Rating", "Rating must be between 1.0 and 5.0");
        }
    }

    /// <summary>
    /// Validates the comment field
    /// </summary>
    private static void ValidateComment(string? comment)
    {
        if (comment != null && comment.Trim().Length > 1000)
        {
            throw new ValidationException("Comment", "Comment cannot exceed 1000 characters");
        }
    }
}



