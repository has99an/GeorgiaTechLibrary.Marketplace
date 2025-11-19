using FluentValidation;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Validator for SearchBooksWithFiltersQuery
/// </summary>
public class SearchBooksWithFiltersQueryValidator : AbstractValidator<SearchBooksWithFiltersQuery>
{
    public SearchBooksWithFiltersQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .MaximumLength(200).WithMessage("Search term cannot exceed 200 characters")
            .Must(NotContainSuspiciousPatterns).WithMessage("Search term contains invalid characters");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("PageSize cannot exceed 100");

        RuleFor(x => x.Genres)
            .Must(list => list == null || list.Count <= 20).WithMessage("Maximum 20 genres allowed");

        RuleFor(x => x.Languages)
            .Must(list => list == null || list.Count <= 10).WithMessage("Maximum 10 languages allowed");

        RuleFor(x => x.Formats)
            .Must(list => list == null || list.Count <= 10).WithMessage("Maximum 10 formats allowed");

        RuleFor(x => x.Conditions)
            .Must(list => list == null || list.Count <= 10).WithMessage("Maximum 10 conditions allowed");

        RuleFor(x => x.Publishers)
            .Must(list => list == null || list.Count <= 20).WithMessage("Maximum 20 publishers allowed");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue).WithMessage("MinPrice must be non-negative");

        RuleFor(x => x.MaxPrice)
            .GreaterThan(x => x.MinPrice).When(x => x.MaxPrice.HasValue && x.MinPrice.HasValue)
            .WithMessage("MaxPrice must be greater than MinPrice");

        RuleFor(x => x.MinRating)
            .InclusiveBetween(0, 5).When(x => x.MinRating.HasValue).WithMessage("MinRating must be between 0 and 5");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortField).WithMessage("Invalid sort field");

        RuleFor(x => x.SortOrder)
            .Must(BeValidSortOrder).WithMessage("Sort order must be 'asc' or 'desc'");
    }

    private bool NotContainSuspiciousPatterns(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        return !InputSanitizer.ContainsSuspiciousPatterns(searchTerm);
    }

    private bool BeValidSortField(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        var validated = InputSanitizer.ValidateSortField(sortBy);
        return validated == sortBy.ToLowerInvariant();
    }

    private bool BeValidSortOrder(string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortOrder))
            return true;

        return sortOrder.ToLowerInvariant() is "asc" or "desc";
    }
}

