using FluentValidation;
using SearchService.Application.Common.Validators;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Validator for GetBookByIsbnQuery
/// </summary>
public class GetBookByIsbnQueryValidator : AbstractValidator<GetBookByIsbnQuery>
{
    public GetBookByIsbnQueryValidator()
    {
        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN is required")
            .Must(BeValidIsbn).WithMessage("Invalid ISBN format. Must be 10 or 13 digits.")
            .Must(HaveValidChecksum).WithMessage("Invalid ISBN checksum");
    }

    private bool BeValidIsbn(string isbn)
    {
        try
        {
            var sanitized = InputSanitizer.SanitizeIsbn(isbn);
            return !string.IsNullOrWhiteSpace(sanitized);
        }
        catch
        {
            return false;
        }
    }

    private bool HaveValidChecksum(string isbn)
    {
        try
        {
            return InputSanitizer.ValidateIsbnChecksum(isbn);
        }
        catch
        {
            return false;
        }
    }
}

