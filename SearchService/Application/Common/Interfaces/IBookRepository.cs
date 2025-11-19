using SearchService.Domain.Entities;
using SearchService.Domain.Specifications;
using SearchService.Domain.ValueObjects;

namespace SearchService.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Book entity
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// Gets a book by ISBN
    /// </summary>
    Task<Book?> GetByIsbnAsync(ISBN isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets books by ISBNs
    /// </summary>
    Task<IEnumerable<Book>> GetByIsbnsAsync(IEnumerable<ISBN> isbns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets books matching a specification
    /// </summary>
    Task<IEnumerable<Book>> GetAsync(ISpecification<Book> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of books matching a specification
    /// </summary>
    Task<int> CountAsync(ISpecification<Book> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a book
    /// </summary>
    Task AddOrUpdateAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a book
    /// </summary>
    Task DeleteAsync(ISBN isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all books (use with caution)
    /// </summary>
    Task<IEnumerable<Book>> GetAllAsync(CancellationToken cancellationToken = default);
}

