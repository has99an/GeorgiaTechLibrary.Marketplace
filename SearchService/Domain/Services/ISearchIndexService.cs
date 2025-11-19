using SearchService.Domain.Entities;
using SearchService.Domain.ValueObjects;

namespace SearchService.Domain.Services;

/// <summary>
/// Domain service for managing the search index
/// </summary>
public interface ISearchIndexService
{
    /// <summary>
    /// Searches for books by terms (tokenized search query)
    /// </summary>
    Task<IEnumerable<ISBN>> SearchByTermsAsync(IEnumerable<string> terms, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a book for search
    /// </summary>
    Task IndexBookAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book from the search index
    /// </summary>
    Task RemoveFromIndexAsync(ISBN isbn, Book? book = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the search index for a book (removes old, adds new)
    /// </summary>
    Task UpdateIndexAsync(Book book, Book? oldBook = null, CancellationToken cancellationToken = default);
}

