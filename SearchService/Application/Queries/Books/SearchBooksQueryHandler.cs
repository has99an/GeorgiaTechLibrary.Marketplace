using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Entities;
using SearchService.Domain.Services;
using System.Text.Json;
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
    private readonly ICacheService _cacheService;
    private readonly ILogger<SearchBooksQueryHandler> _logger;
    private static readonly Regex WordRegex = new(@"\w+", RegexOptions.Compiled);

    public SearchBooksQueryHandler(
        ISearchIndexService searchIndex,
        IFuzzySearchService fuzzySearch,
        IBookRepository repository,
        IMapper mapper,
        ICacheService cacheService,
        ILogger<SearchBooksQueryHandler> logger)
    {
        _searchIndex = searchIndex;
        _fuzzySearch = fuzzySearch;
        _repository = repository;
        _mapper = mapper;
        _cacheService = cacheService;
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
            return new SearchBooksResult(new PagedResult<BookSellerDto>(Enumerable.Empty<BookSellerDto>(), page, pageSize, 0));
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
                    new PagedResult<BookSellerDto>(Enumerable.Empty<BookSellerDto>(), page, pageSize, 0),
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

        // Sort based on request (sort books first, then we'll expand to seller entries)
        var sortedBooks = request.SortBy?.ToLower() switch
        {
            "title" => scoredBooks.OrderBy(x => x.Book.Title),
            "price" => scoredBooks.OrderBy(x => x.Book.Pricing.MinPrice),
            "rating" => scoredBooks.OrderByDescending(x => x.Book.Rating),
            _ => scoredBooks.OrderByDescending(x => x.Score) // Default: relevance
        };

        // Map books to DTOs first
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(sortedBooks.Select(x => x.Book));
        var bookDtosList = bookDtos.ToList();

        // For each book, get its sellers and create individual BookSellerDto entries
        var bookSellerEntries = new List<BookSellerDto>();
        
        _logger.LogDebug("Processing {Count} books to create individual seller entries", bookDtosList.Count);
        
        foreach (var bookDto in bookDtosList)
        {
            // Get sellers for this book from Redis
            var sellers = await GetSellersForBookAsync(bookDto.Isbn, cancellationToken);
            
            if (sellers.Any())
            {
                // Create a BookSellerDto entry for each seller
                foreach (var seller in sellers)
                {
                    // Only include sellers with available stock
                    if (seller.Quantity > 0)
                    {
                        bookSellerEntries.Add(new BookSellerDto
                        {
                            // Book information
                            Isbn = bookDto.Isbn,
                            Title = bookDto.Title,
                            Author = bookDto.Author,
                            YearOfPublication = bookDto.YearOfPublication,
                            Publisher = bookDto.Publisher,
                            ImageUrlS = bookDto.ImageUrlS,
                            ImageUrlM = bookDto.ImageUrlM,
                            ImageUrlL = bookDto.ImageUrlL,
                            Genre = bookDto.Genre,
                            Language = bookDto.Language,
                            PageCount = bookDto.PageCount,
                            Description = bookDto.Description,
                            Rating = bookDto.Rating,
                            AvailabilityStatus = bookDto.AvailabilityStatus,
                            Edition = bookDto.Edition,
                            Format = bookDto.Format,
                            
                            // Seller-specific information
                            SellerId = seller.SellerId,
                            Price = seller.Price,
                            Quantity = seller.Quantity,
                            Condition = seller.Condition,
                            LastUpdated = seller.LastUpdated,
                            SellerName = seller.SellerName
                        });
                    }
                }
            }
            else
            {
                // If no sellers found, still include the book but without seller-specific info
                // This allows books to appear in search even if they don't have sellers yet
                bookSellerEntries.Add(new BookSellerDto
                {
                    Isbn = bookDto.Isbn,
                    Title = bookDto.Title,
                    Author = bookDto.Author,
                    YearOfPublication = bookDto.YearOfPublication,
                    Publisher = bookDto.Publisher,
                    ImageUrlS = bookDto.ImageUrlS,
                    ImageUrlM = bookDto.ImageUrlM,
                    ImageUrlL = bookDto.ImageUrlL,
                    Genre = bookDto.Genre,
                    Language = bookDto.Language,
                    PageCount = bookDto.PageCount,
                    Description = bookDto.Description,
                    Rating = bookDto.Rating,
                    AvailabilityStatus = bookDto.AvailabilityStatus,
                    Edition = bookDto.Edition,
                    Format = bookDto.Format,
                    SellerId = string.Empty,
                    Price = 0,
                    Quantity = 0,
                    Condition = string.Empty,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        // Apply sorting to seller entries if needed (especially for price sorting)
        if (request.SortBy?.ToLower() == "price")
        {
            bookSellerEntries = bookSellerEntries.OrderBy(x => x.Price).ToList();
        }

        var sellerEntriesCount = bookSellerEntries.Count;
        
        // Calculate estimated total count for pagination
        // Since we have multiple seller entries per book, we need to estimate the total
        double avgSellersPerBook = 0.0;
        if (bookDtosList.Count > 0 && sellerEntriesCount > 0)
        {
            avgSellersPerBook = (double)sellerEntriesCount / bookDtosList.Count;
        }
        
        var estimatedTotalSellerEntries = books.Count > 0 && avgSellersPerBook > 0
            ? (int)Math.Ceiling(books.Count * avgSellersPerBook)
            : sellerEntriesCount;

        // Apply pagination to seller entries
        var pagedSellerEntries = bookSellerEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pagedResult = new PagedResult<BookSellerDto>(pagedSellerEntries, page, pageSize, estimatedTotalSellerEntries);

        _logger.LogInformation("Found {TotalBooks} books ({TotalSellerEntries} seller entries) for search term: {SearchTerm}, returning page {Page} with {Count} seller entries", 
            books.Count, sellerEntriesCount, request.SearchTerm, page, pagedSellerEntries.Count);

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

    /// <summary>
    /// Gets all sellers for a specific book from Redis cache
    /// </summary>
    private async Task<IEnumerable<SellerInfoDto>> GetSellersForBookAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"sellers:{isbn}";
            var sellersJson = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(sellersJson))
            {
                _logger.LogDebug("No sellers found in cache for ISBN: {Isbn}", isbn);
                return Enumerable.Empty<SellerInfoDto>();
            }

            var sellers = JsonSerializer.Deserialize<List<SellerInfoDto>>(sellersJson) ?? new List<SellerInfoDto>();
            _logger.LogDebug("Found {Count} sellers for ISBN: {Isbn}", sellers.Count, isbn);
            
            return sellers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sellers for ISBN: {Isbn}", isbn);
            return Enumerable.Empty<SellerInfoDto>();
        }
    }
}

