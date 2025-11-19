using FluentValidation;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Queries.Analytics;

/// <summary>
/// Validator for GetPopularSearchesQuery
/// </summary>
public class GetPopularSearchesQueryValidator : AbstractValidator<GetPopularSearchesQuery>
{
    public GetPopularSearchesQueryValidator()
    {
        RuleFor(x => x.TopN)
            .GreaterThan(0).WithMessage("TopN must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("TopN cannot exceed 100");

        RuleFor(x => x.TimeWindow)
            .Must(BeValidTimeWindow).WithMessage("Invalid time window. Allowed values: 24h, 7d, 30d, all");
    }

    private bool BeValidTimeWindow(string timeWindow)
    {
        var validated = InputSanitizer.ValidateTimeWindow(timeWindow);
        return validated == timeWindow.ToLowerInvariant();
    }
}

