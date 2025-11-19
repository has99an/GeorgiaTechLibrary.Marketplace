namespace OrderService.Domain.Exceptions;

/// <summary>
/// Exception thrown for shopping cart related errors
/// </summary>
public class ShoppingCartException : DomainException
{
    public ShoppingCartException(string message) : base(message)
    {
    }

    public ShoppingCartException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

