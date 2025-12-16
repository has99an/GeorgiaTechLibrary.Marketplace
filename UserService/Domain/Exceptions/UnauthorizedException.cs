namespace UserService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user attempts an unauthorized action
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}




