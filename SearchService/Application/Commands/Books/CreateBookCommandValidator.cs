using FluentValidation;

namespace SearchService.Application.Commands.Books;

/// <summary>
/// Validator for CreateBookCommand
/// </summary>
public class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(x => x.ISBN)
            .NotEmpty().WithMessage("ISBN is required")
            .Length(10, 13).WithMessage("ISBN must be 10 or 13 characters");

        RuleFor(x => x.BookTitle)
            .NotEmpty().WithMessage("Book title is required")
            .MaximumLength(500).WithMessage("Book title cannot exceed 500 characters");

        RuleFor(x => x.BookAuthor)
            .NotEmpty().WithMessage("Book author is required")
            .MaximumLength(200).WithMessage("Book author cannot exceed 200 characters");

        RuleFor(x => x.YearOfPublication)
            .GreaterThanOrEqualTo(0).WithMessage("Year of publication must be valid")
            .LessThanOrEqualTo(DateTime.Now.Year + 1).WithMessage("Year of publication cannot be in the future");

        RuleFor(x => x.Publisher)
            .MaximumLength(200).WithMessage("Publisher cannot exceed 200 characters");

        RuleFor(x => x.Rating)
            .GreaterThanOrEqualTo(0).WithMessage("Rating must be greater than or equal to 0")
            .LessThanOrEqualTo(5).WithMessage("Rating cannot exceed 5");

        RuleFor(x => x.PageCount)
            .GreaterThanOrEqualTo(0).WithMessage("Page count must be greater than or equal to 0");
    }
}

