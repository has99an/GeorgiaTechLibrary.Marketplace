using SearchService.Domain.Entities;

namespace SearchService.Domain.Specifications;

/// <summary>
/// Specification for querying books by price range
/// </summary>
public class BooksByPriceRangeSpecification : BaseSpecification<Book>
{
    public BooksByPriceRangeSpecification(decimal minPrice, decimal maxPrice)
        : base(book => book.Pricing.MinPrice >= minPrice && book.Pricing.MinPrice <= maxPrice)
    {
        ApplyOrderBy(b => b.Pricing.MinPrice);
    }
}

