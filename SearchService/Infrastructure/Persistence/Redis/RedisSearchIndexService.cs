using SearchService.Domain.Entities;
using SearchService.Domain.Services;
using SearchService.Domain.ValueObjects;
using StackExchange.Redis;
using System.Text.RegularExpressions;

namespace SearchService.Infrastructure.Persistence.Redis;

/// <summary>
/// Redis implementation of ISearchIndexService
/// </summary>
public class RedisSearchIndexService : ISearchIndexService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisSearchIndexService> _logger;
    private static readonly Regex WordRegex = new(@"\w+", RegexOptions.Compiled);

    public RedisSearchIndexService(
        IConnectionMultiplexer redis,
        ILogger<RedisSearchIndexService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<IEnumerable<ISBN>> SearchByTermsAsync(IEnumerable<string> terms, CancellationToken cancellationToken = default)
    {
        try
        {
            var termsList = terms.ToList();
            
            if (!termsList.Any())
                return Enumerable.Empty<ISBN>();

            RedisValue[] isbnValues;

            if (termsList.Count == 1)
            {
                // Single word search
                var indexKey = $"index:{termsList[0]}";
                isbnValues = await _database.SetMembersAsync(indexKey);
            }
            else
            {
                // Multi-word search - intersect all word sets
                var indexKeys = termsList.Select(w => (RedisKey)$"index:{w}").ToArray();
                isbnValues = await _database.SetCombineAsync(SetOperation.Intersect, indexKeys);
            }

            if (!isbnValues.Any())
                return Enumerable.Empty<ISBN>();

            return isbnValues.Select(v => ISBN.Create(v.ToString())).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by terms");
            return Enumerable.Empty<ISBN>();
        }
    }

    public async Task IndexBookAsync(Book book, CancellationToken cancellationToken = default)
    {
        try
        {
            var words = TokenizeBook(book).ToList();

            if (!words.Any())
                return;

            // Use Redis batch for efficient indexing (single round-trip)
            var batch = _database.CreateBatch();
            var tasks = words.Select(word =>
            {
                var indexKey = $"index:{word}";
                return batch.SetAddAsync(indexKey, book.Isbn.Value);
            }).ToList();

            // Execute batch
            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation("Indexed book with ISBN {Isbn} ({WordCount} terms) using batch operation", 
                book.Isbn.Value, words.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing book with ISBN {Isbn}", book.Isbn.Value);
        }
    }

    public async Task RemoveFromIndexAsync(ISBN isbn, Book? book = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (book != null)
            {
                var words = TokenizeBook(book).ToList();

                if (words.Any())
                {
                    // Use Redis batch for efficient removal (single round-trip)
                    var batch = _database.CreateBatch();
                    var tasks = words.Select(word =>
                    {
                        var indexKey = $"index:{word}";
                        return batch.SetRemoveAsync(indexKey, isbn.Value);
                    }).ToList();

                    // Execute batch
                    batch.Execute();
                    await Task.WhenAll(tasks);
                }
            }

            _logger.LogInformation("Removed book from index with ISBN {Isbn} using batch operation", isbn.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing book from index with ISBN {Isbn}", isbn.Value);
        }
    }

    public async Task UpdateIndexAsync(Book book, Book? oldBook = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Remove old index entries if we have the old book
            if (oldBook != null)
            {
                await RemoveFromIndexAsync(book.Isbn, oldBook, cancellationToken);
            }

            // Add new index entries
            await IndexBookAsync(book, cancellationToken);

            _logger.LogInformation("Updated index for book with ISBN {Isbn}", book.Isbn.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating index for book with ISBN {Isbn}", book.Isbn.Value);
        }
    }

    private IEnumerable<string> TokenizeBook(Book book)
    {
        var terms = new List<string>();

        // Tokenize title
        if (!string.IsNullOrWhiteSpace(book.Title))
        {
            terms.AddRange(Tokenize(book.Title));
        }

        // Tokenize author
        if (!string.IsNullOrWhiteSpace(book.Author))
        {
            terms.AddRange(Tokenize(book.Author));
        }

        // Add ISBN
        terms.Add(book.Isbn.Value.ToLowerInvariant());

        return terms.Distinct();
    }

    private IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Empty<string>();

        return WordRegex.Matches(text.ToLowerInvariant())
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct();
    }
}

