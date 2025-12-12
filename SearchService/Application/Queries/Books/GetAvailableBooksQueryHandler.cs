using AutoMapper;
using MediatR;
using SearchService.Application.Common.Interfaces;
using SearchService.Application.Common.Models;
using SearchService.Domain.Specifications;
using System.Text.Json;

namespace SearchService.Application.Queries.Books;

/// <summary>
/// Handler for GetAvailableBooksQuery
/// Returns individual seller entries instead of aggregated books
/// </summary>
public class GetAvailableBooksQueryHandler : IRequestHandler<GetAvailableBooksQuery, GetAvailableBooksResult>
{
    private readonly IBookRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetAvailableBooksQueryHandler> _logger;

    public GetAvailableBooksQueryHandler(
        IBookRepository repository,
        IMapper mapper,
        ICacheService cacheService,
        ILogger<GetAvailableBooksQueryHandler> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<GetAvailableBooksResult> Handle(GetAvailableBooksQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting available books: Page {Page}, PageSize {PageSize}, SortBy {SortBy}, SortOrder {SortOrder}",
            request.Page, request.PageSize, request.SortBy, request.SortOrder);

        // Validate pagination parameters
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var descending = request.SortOrder?.ToLower() == "desc";

        // Create specification
        var spec = new AvailableBooksSpecification(page, pageSize, request.SortBy, descending);
        _logger.LogDebug("Created specification: Skip={Skip}, Take={Take}, OrderBy={OrderBy}, OrderByDescending={OrderByDescending}",
            spec.Skip, spec.Take, spec.OrderBy?.ToString() ?? "null", spec.OrderByDescending?.ToString() ?? "null");

        // Get books and count
        _logger.LogDebug("Calling repository.GetAsync() with specification");
        var books = await _repository.GetAsync(spec, cancellationToken);
        var booksList = books.ToList();
        _logger.LogInformation("Repository returned {Count} books", booksList.Count);

        _logger.LogDebug("Calling repository.CountAsync() with specification");
        var totalCount = await _repository.CountAsync(spec, cancellationToken);
        _logger.LogInformation("Repository returned total count: {TotalCount}", totalCount);

        // Map books to DTOs
        var bookDtos = _mapper.Map<IEnumerable<BookDto>>(booksList);
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
                _logger.LogDebug("No sellers found for book ISBN: {Isbn}", bookDto.Isbn);
            }
        }

        _logger.LogInformation("Created {Count} seller entries from {BookCount} books", bookSellerEntries.Count, bookDtosList.Count);

        // Calculate estimated total seller entries for accurate pagination
        // totalCount from repository is the number of books, not seller entries
        // Since each book can have multiple sellers, we estimate based on average sellers per book
        var sellerEntriesCount = bookSellerEntries.Count;
        double avgSellersPerBook = 0.0;
        
        // Calculate average sellers per book from current page (avoid division by zero)
        if (bookDtosList.Count > 0 && sellerEntriesCount > 0)
        {
            avgSellersPerBook = (double)sellerEntriesCount / bookDtosList.Count;
            _logger.LogDebug("Average sellers per book on current page: {AvgSellersPerBook:F2} ({SellerEntries} entries / {BookCount} books)",
                avgSellersPerBook, sellerEntriesCount, bookDtosList.Count);
        }
        else if (bookDtosList.Count > 0 && sellerEntriesCount == 0)
        {
            _logger.LogDebug("No seller entries found for {BookCount} books on current page", bookDtosList.Count);
        }

        // Estimate total seller entries based on total book count and average sellers per book
        // Edge cases:
        // - If no books: use sellerEntriesCount (will be 0)
        // - If no sellers per book: use sellerEntriesCount (current page count)
        // - If totalCount is 0: use sellerEntriesCount (current page count)
        var estimatedTotalSellerEntries = totalCount > 0 && avgSellersPerBook > 0
            ? (int)Math.Ceiling(totalCount * avgSellersPerBook)
            : sellerEntriesCount; // Fallback to current count if estimation not possible

        _logger.LogInformation("Pagination: {SellerEntriesOnPage} seller entries on page {Page}, estimated {EstimatedTotal} total entries (based on {TotalBooks} books × {AvgSellers:F2} avg sellers/book). HasNextPage: {HasNextPage}",
            sellerEntriesCount, page, estimatedTotalSellerEntries, totalCount, avgSellersPerBook, 
            page < (int)Math.Ceiling((double)estimatedTotalSellerEntries / pageSize));

        var pagedResult = new PagedResult<BookSellerDto>(bookSellerEntries, page, pageSize, estimatedTotalSellerEntries);

        _logger.LogInformation("Returning {Count} seller entries (Page {Page} of {TotalPages})",
            sellerEntriesCount, page, pagedResult.TotalPages);

        if (sellerEntriesCount == 0 && totalCount == 0)
        {
            _logger.LogWarning("No available books found. This may indicate: 1) No books in Redis, 2) No books have stock > 0, 3) Sorted sets are empty. Check /search/debug endpoint for details.");
        }
        else if (sellerEntriesCount == 0 && totalCount > 0)
        {
            _logger.LogWarning("Total count is {TotalCount} but no seller entries returned. This may indicate that books exist but have no sellers with stock.", totalCount);
        }

        return new GetAvailableBooksResult(pagedResult);
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

