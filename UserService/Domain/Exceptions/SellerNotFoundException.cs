namespace UserService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a seller profile is not found
/// </summary>
public class SellerNotFoundException : DomainException
{
    public SellerNotFoundException(Guid sellerId) 
        : base($"Seller profile with ID {sellerId} not found")
    {
    }

    public SellerNotFoundException(string message) 
        : base(message)
    {
    }
}





