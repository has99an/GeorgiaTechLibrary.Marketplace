using FluentValidation;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Validator for GetAvailableBooksQuery
/// </summary>
public class GetAvailableBooksQueryValidator : AbstractValidator<GetAvailableBooksQuery>
{
    public GetAvailableBooksQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy == null || sortBy.ToLower() == "title" || sortBy.ToLower() == "price")
            .WithMessage("Sort by must be either 'title' or 'price'");

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => sortOrder == null || sortOrder.ToLower() == "asc" || sortOrder.ToLower() == "desc")
            .WithMessage("Sort order must be either 'asc' or 'desc'");
    }
}

