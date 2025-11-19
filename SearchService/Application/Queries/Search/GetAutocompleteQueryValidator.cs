using FluentValidation;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Queries.Search;

/// <summary>
/// Validator for GetAutocompleteQuery with security enhancements
/// </summary>
public class GetAutocompleteQueryValidator : AbstractValidator<GetAutocompleteQuery>
{
    public GetAutocompleteQueryValidator()
    {
        RuleFor(x => x.Prefix)
            .NotEmpty()
            .WithMessage("Prefix cannot be empty")
            .MinimumLength(1)
            .WithMessage("Prefix must be at least 1 character")
            .MaximumLength(50)
            .WithMessage("Prefix cannot exceed 50 characters")
            .Must(NotContainSuspiciousPatterns)
            .WithMessage("Prefix contains invalid characters");

        RuleFor(x => x.MaxResults)
            .GreaterThan(0)
            .WithMessage("MaxResults must be greater than 0")
            .LessThanOrEqualTo(50)
            .WithMessage("MaxResults cannot exceed 50");
    }

    private bool NotContainSuspiciousPatterns(string prefix)
    {
        return !InputSanitizer.ContainsSuspiciousPatterns(prefix);
    }
}

