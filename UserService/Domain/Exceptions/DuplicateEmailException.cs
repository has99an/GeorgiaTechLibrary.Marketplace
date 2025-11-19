namespace UserService.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a user with an email that already exists
/// </summary>
public class DuplicateEmailException : DomainException
{
    public string Email { get; }

    public DuplicateEmailException(string email)
        : base($"A user with email '{email}' already exists")
    {
        Email = email;
    }
}

