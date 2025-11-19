namespace UserService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user is not found
/// </summary>
public class UserNotFoundException : DomainException
{
    public Guid UserId { get; }

    public UserNotFoundException(Guid userId)
        : base($"User with ID '{userId}' was not found")
    {
        UserId = userId;
    }

    public UserNotFoundException(string email)
        : base($"User with email '{email}' was not found")
    {
        UserId = Guid.Empty;
    }
}

