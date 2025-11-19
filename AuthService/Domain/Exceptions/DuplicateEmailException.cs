namespace AuthService.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting to register with an email that already exists
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

