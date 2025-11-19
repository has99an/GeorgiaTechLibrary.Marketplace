namespace AuthService.Domain.Exceptions;

/// <summary>
/// Exception thrown when login credentials are invalid
/// </summary>
public class InvalidCredentialsException : AuthenticationException
{
    public string Email { get; }

    public InvalidCredentialsException(string email) 
        : base("Invalid email or password")
    {
        Email = email;
    }
}

