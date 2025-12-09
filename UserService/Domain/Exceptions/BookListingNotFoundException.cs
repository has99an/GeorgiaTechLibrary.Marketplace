namespace UserService.Domain.Exceptions;

/// <summary>
/// Exception thrown when a book listing is not found
/// </summary>
public class BookListingNotFoundException : DomainException
{
    public BookListingNotFoundException(Guid listingId) 
        : base($"Book listing with ID {listingId} not found")
    {
    }

    public BookListingNotFoundException(string message) 
        : base(message)
    {
    }
}


