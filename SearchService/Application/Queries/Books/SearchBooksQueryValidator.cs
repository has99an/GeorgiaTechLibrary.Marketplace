using FluentValidation;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Validator for SearchBooksQuery with security enhancements
/// </summary>
public class SearchBooksQueryValidator : AbstractValidator<SearchBooksQuery>
{
    public SearchBooksQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty().WithMessage("Search term is required")
            .MinimumLength(2).WithMessage("Search term must be at least 2 characters")
            .MaximumLength(200).WithMessage("Search term cannot exceed 200 characters")
            .Must(NotContainSuspiciousPatterns).WithMessage("Search term contains invalid characters");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("PageSize cannot exceed 100");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortField).WithMessage("Invalid sort field. Allowed: relevance, title, price, rating");
    }

    private bool NotContainSuspiciousPatterns(string searchTerm)
    {
        return !InputSanitizer.ContainsSuspiciousPatterns(searchTerm);
    }

    private bool BeValidSortField(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        var validated = InputSanitizer.ValidateSortField(sortBy);
        return validated == sortBy.ToLowerInvariant();
    }
}

