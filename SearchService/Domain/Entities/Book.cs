using SearchService.Domain.ValueObjects;

namespace SearchService.Domain.Entities;

/// <summary>
/// Domain entity representing a Book in the search index
/// </summary>
public class Book
{
    public ISBN Isbn { get; private set; }
    public string Title { get; private set; }
    public string Author { get; private set; }
    public int YearOfPublication { get; private set; }
    public string Publisher { get; private set; }
    public string? ImageUrlS { get; private set; }
    public string? ImageUrlM { get; private set; }
    public string? ImageUrlL { get; private set; }
    public StockInfo Stock { get; private set; }
    public PriceInfo Pricing { get; private set; }
    public List<string> AvailableConditions { get; private set; }
    
    // Extended metadata
    public string Genre { get; private set; }
    public string Language { get; private set; }
    public int PageCount { get; private set; }
    public string Description { get; private set; }
    public double Rating { get; private set; }
    public string AvailabilityStatus { get; private set; }
    public string Edition { get; private set; }
    public string Format { get; private set; }

    private Book(
        ISBN isbn,
        string title,
        string author,
        int yearOfPublication,
        string publisher,
        string? imageUrlS,
        string? imageUrlM,
        string? imageUrlL,
        StockInfo stock,
        PriceInfo pricing,
        List<string> availableConditions,
        string genre,
        string language,
        int pageCount,
        string description,
        double rating,
        string availabilityStatus,
        string edition,
        string format)
    {
        Isbn = isbn;
        Title = title;
        Author = author;
        YearOfPublication = yearOfPublication;
        Publisher = publisher;
        ImageUrlS = imageUrlS;
        ImageUrlM = imageUrlM;
        ImageUrlL = imageUrlL;
        Stock = stock;
        Pricing = pricing;
        AvailableConditions = availableConditions;
        Genre = genre;
        Language = language;
        PageCount = pageCount;
        Description = description;
        Rating = rating;
        AvailabilityStatus = availabilityStatus;
        Edition = edition;
        Format = format;
    }

    /// <summary>
    /// Factory method to create a new Book
    /// </summary>
    public static Book Create(
        ISBN isbn,
        string title,
        string author,
        int yearOfPublication,
        string publisher,
        string? imageUrlS = null,
        string? imageUrlM = null,
        string? imageUrlL = null,
        string genre = "",
        string language = "English",
        int pageCount = 0,
        string description = "",
        double rating = 0.0,
        string availabilityStatus = "Available",
        string edition = "",
        string format = "Paperback")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author cannot be empty", nameof(author));

        if (yearOfPublication < 0)
            throw new ArgumentException("Year of publication cannot be negative", nameof(yearOfPublication));

        return new Book(
            isbn,
            title,
            author,
            yearOfPublication,
            publisher ?? string.Empty,
            imageUrlS,
            imageUrlM,
            imageUrlL,
            StockInfo.Empty(),
            PriceInfo.Empty(),
            new List<string>(),
            genre,
            language,
            pageCount,
            description,
            rating,
            availabilityStatus,
            edition,
            format);
    }

    /// <summary>
    /// Updates the stock information for this book
    /// </summary>
    public void UpdateStock(int totalStock, int availableSellers, decimal minPrice)
    {
        Stock = StockInfo.Create(totalStock, availableSellers);
        
        if (minPrice > 0)
        {
            Pricing = PriceInfo.FromSinglePrice(minPrice);
        }
        
        UpdateAvailabilityStatus();
    }

    /// <summary>
    /// Updates pricing information
    /// </summary>
    public void UpdatePricing(decimal minPrice, decimal maxPrice, decimal averagePrice)
    {
        Pricing = PriceInfo.Create(minPrice, maxPrice, averagePrice);
    }

    /// <summary>
    /// Updates available conditions
    /// </summary>
    public void UpdateConditions(List<string> conditions)
    {
        AvailableConditions = conditions ?? new List<string>();
    }

    /// <summary>
    /// Updates book metadata
    /// </summary>
    public void UpdateMetadata(
        string? title = null,
        string? author = null,
        int? yearOfPublication = null,
        string? publisher = null,
        string? imageUrlS = null,
        string? imageUrlM = null,
        string? imageUrlL = null,
        string? genre = null,
        string? language = null,
        int? pageCount = null,
        string? description = null,
        double? rating = null,
        string? edition = null,
        string? format = null)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title;
        if (!string.IsNullOrWhiteSpace(author)) Author = author;
        if (yearOfPublication.HasValue) YearOfPublication = yearOfPublication.Value;
        if (!string.IsNullOrWhiteSpace(publisher)) Publisher = publisher;
        if (imageUrlS != null) ImageUrlS = imageUrlS;
        if (imageUrlM != null) ImageUrlM = imageUrlM;
        if (imageUrlL != null) ImageUrlL = imageUrlL;
        if (!string.IsNullOrWhiteSpace(genre)) Genre = genre;
        if (!string.IsNullOrWhiteSpace(language)) Language = language;
        if (pageCount.HasValue) PageCount = pageCount.Value;
        if (!string.IsNullOrWhiteSpace(description)) Description = description;
        if (rating.HasValue) Rating = rating.Value;
        if (!string.IsNullOrWhiteSpace(edition)) Edition = edition;
        if (!string.IsNullOrWhiteSpace(format)) Format = format;
    }

    /// <summary>
    /// Checks if the book is available for purchase
    /// </summary>
    public bool IsAvailable() => Stock.IsAvailable();

    private void UpdateAvailabilityStatus()
    {
        AvailabilityStatus = IsAvailable() ? "Available" : "Out of Stock";
    }

    /// <summary>
    /// Gets search terms for indexing
    /// </summary>
    public IEnumerable<string> GetSearchTerms()
    {
        var terms = new List<string>();
        
        // Add title words
        if (!string.IsNullOrWhiteSpace(Title))
            terms.AddRange(Title.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        
        // Add author words
        if (!string.IsNullOrWhiteSpace(Author))
            terms.AddRange(Author.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        
        // Add ISBN
        terms.Add(Isbn.Value);
        
        return terms.Select(t => t.ToLowerInvariant()).Distinct();
    }
}

