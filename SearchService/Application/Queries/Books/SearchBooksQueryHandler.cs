using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Entities;
using SearchService.Domain.Services;
using System.Text.RegularExpressions;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Optimized handler for SearchBooksQuery with pagination, ranking, fuzzy search, and parallel processing
/// </summary>
public class SearchBooksQueryHandler : IRequestHandler<SearchBooksQuery, SearchBooksResult>
{
    private readonly ISearchIndexService _searchIndex;
    private readonly IFuzzySearchService _fuzzySearch;
    private readonly IBookRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<SearchBooksQueryHandler> _logger;
    private static readonly Regex WordRegex = new(@"\w+", RegexOptions.Compiled);

    public SearchBooksQueryHandler(
        ISearchIndexService searchIndex,
        IFuzzySearchService fuzzySearch,
        IBookRepository repository,
        IMapper mapper,
        ILogger<SearchBooksQueryHandler> logger)
    {
        _searchIndex = searchIndex;
        _fuzzySearch = fuzzySearch;
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SearchBooksResult> Handle(SearchBooksQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching for books with term: {SearchTerm}, Page: {Page}, PageSize: {PageSize}, SortBy: {SortBy}", 
            request.SearchTerm, request.Page, request.PageSize, request.SortBy);

        // Validate pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Tokenize search term
        var terms = TokenizeSearchTerm(request.SearchTerm).ToList();
        
        if (!terms.Any())
        {
            _logger.LogWarning("No valid search terms found in: {SearchTerm}", request.SearchTerm);
            return new SearchBooksResult(new PagedResult<BookDto>(Enumerable.Empty<BookDto>(), page, pageSize, 0));
        }

        // Search index for ISBNs
        var isbns = (await _searchIndex.SearchByTermsAsync(terms, cancellationToken)).ToList();
        List<string>? suggestions = null;
        
        // If no results, try fuzzy search
        if (!isbns.Any())
        {
            _logger.LogInformation("No exact matches found, trying fuzzy search for: {SearchTerm}", request.SearchTerm);
            
            var fuzzyIsbns = await _fuzzySearch.FuzzySearchAsync(request.SearchTerm);
            isbns = fuzzyIsbns.ToList();
            
            // Generate "Did you mean?" suggestions
            suggestions = new List<string>();
            foreach (var term in terms)
            {
                var similarTerms = await _fuzzySearch.GetSimilarTermsAsync(term);
                suggestions.AddRange(similarTerms);
            }
            
            if (!isbns.Any())
            {
                _logger.LogInformation("No books found even with fuzzy search for: {SearchTerm}", request.SearchTerm);
                return new SearchBooksResult(
                    new PagedResult<BookDto>(Enumerable.Empty<BookDto>(), page, pageSize, 0),
                    suggestions.Distinct()
                );
            }
        }

        // Fetch books (optimized with MGET and parallel deserialization)
        var books = (await _repository.GetByIsbnsAsync(isbns, cancellationToken)).ToList();
        
        // Calculate relevance scores
        var scoredBooks = books.Select(book => new
        {
            Book = book,
            Score = CalculateRelevanceScore(book, terms)
        }).ToList();

        // Sort based on request
        var sortedBooks = request.SortBy?.ToLower() switch
        {
            "title" => scoredBooks.OrderBy(x => x.Book.Title),
            "price" => scoredBooks.OrderBy(x => x.Book.Pricing.MinPrice),
            "rating" => scoredBooks.OrderByDescending(x => x.Book.Rating),
            _ => scoredBooks.OrderByDescending(x => x.Score) // Default: relevance
        };

        var totalCount = scoredBooks.Count;
        
        // Apply pagination
        var pagedBooks = sortedBooks
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Book)
            .ToList();

        // Map to DTOs
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(pagedBooks);
        var pagedResult = new PagedResult<BookDto>(bookDtos, page, pageSize, totalCount);

        _logger.LogInformation("Found {Total} books for search term: {SearchTerm}, returning page {Page} with {Count} results", 
            totalCount, request.SearchTerm, page, bookDtos.Count());

        return new SearchBooksResult(pagedResult, suggestions);
    }

    private IEnumerable<string> TokenizeSearchTerm(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<string>();

        return WordRegex.Matches(searchTerm.ToLowerInvariant())
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct();
    }

    private double CalculateRelevanceScore(Book book, List<string> searchTerms)
    {
        double score = 0;

        var titleLower = book.Title.ToLowerInvariant();
        var authorLower = book.Author.ToLowerInvariant();
        var isbnLower = book.Isbn.Value.ToLowerInvariant();

        foreach (var term in searchTerms)
        {
            // Exact title match: 10x boost
            if (titleLower == term)
                score += 100;
            // Title starts with term: 5x boost
            else if (titleLower.StartsWith(term))
                score += 50;
            // Title contains term: 3x boost
            else if (titleLower.Contains(term))
                score += 30;

            // Author match: 3x boost
            if (authorLower.Contains(term))
                score += 30;

            // ISBN match: 2x boost
            if (isbnLower.Contains(term))
                score += 20;
        }

        // Boost by rating (0-5 scale)
        score += book.Rating * 2;

        // Boost by availability
        if (book.IsAvailable())
            score += 10;

        // Boost by stock level
        score += Math.Min(book.Stock.TotalStock * 0.1, 5);

        return score;
    }
}

