namespace SearchService.Domain.ValueObjects;

/// <summary>
/// Value object encapsulating pricing information
/// </summary>
public sealed class PriceInfo : IEquatable<PriceInfo>
{
    public decimal MinPrice { get; }
    public decimal MaxPrice { get; }
    public decimal AveragePrice { get; }

    private PriceInfo(decimal minPrice, decimal maxPrice, decimal averagePrice)
    {
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        AveragePrice = averagePrice;
    }

    public static PriceInfo Create(decimal minPrice, decimal maxPrice, decimal averagePrice)
    {
        if (minPrice < 0)
            throw new ArgumentException("Min price cannot be negative", nameof(minPrice));

        if (maxPrice < 0)
            throw new ArgumentException("Max price cannot be negative", nameof(maxPrice));

        if (averagePrice < 0)
            throw new ArgumentException("Average price cannot be negative", nameof(averagePrice));

        if (minPrice > maxPrice)
            throw new ArgumentException("Min price cannot be greater than max price");

        return new PriceInfo(minPrice, maxPrice, averagePrice);
    }

    public static PriceInfo FromSinglePrice(decimal price)
    {
        if (price < 0)
            throw new ArgumentException("Price cannot be negative", nameof(price));

        return new PriceInfo(price, price, price);
    }

    public static PriceInfo Empty() => new PriceInfo(0, 0, 0);

    public bool HasPrice() => MinPrice > 0;

    public bool Equals(PriceInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return MinPrice == other.MinPrice && MaxPrice == other.MaxPrice && AveragePrice == other.AveragePrice;
    }

    public override bool Equals(object? obj) => Equals(obj as PriceInfo);

    public override int GetHashCode() => HashCode.Combine(MinPrice, MaxPrice, AveragePrice);

    public static bool operator ==(PriceInfo? left, PriceInfo? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(PriceInfo? left, PriceInfo? right) => !(left == right);
}

