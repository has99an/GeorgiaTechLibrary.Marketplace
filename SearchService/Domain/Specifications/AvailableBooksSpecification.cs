using SearchService.Domain.Entities;

namespace SearchService.Domain.Specifications;

/// <summary>
/// Specification for querying available books
/// </summary>
public class AvailableBooksSpecification : BaseSpecification<Book>
{
    public AvailableBooksSpecification(int page, int pageSize, string? sortBy = null, bool descending = false)
        : base(book => book.Stock.TotalStock > 0 && book.Stock.AvailableSellers > 0)
    {
        ApplyPaging((page - 1) * pageSize, pageSize);

        if (sortBy?.ToLower() == "price")
        {
            if (descending)
                ApplyOrderByDescending(b => b.Pricing.MinPrice);
            else
                ApplyOrderBy(b => b.Pricing.MinPrice);
        }
        else // Default to title
        {
            if (descending)
                ApplyOrderByDescending(b => b.Title);
            else
                ApplyOrderBy(b => b.Title);
        }
    }
}

