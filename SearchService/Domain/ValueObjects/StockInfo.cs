namespace SearchService.Domain.ValueObjects;

/// <summary>
/// Value object encapsulating stock information
/// </summary>
public sealed class StockInfo : IEquatable<StockInfo>
{
    public int TotalStock { get; }
    public int AvailableSellers { get; }
    public DateTime LastUpdated { get; }

    private StockInfo(int totalStock, int availableSellers, DateTime lastUpdated)
    {
        TotalStock = totalStock;
        AvailableSellers = availableSellers;
        LastUpdated = lastUpdated;
    }

    public static StockInfo Create(int totalStock, int availableSellers)
    {
        if (totalStock < 0)
            throw new ArgumentException("Total stock cannot be negative", nameof(totalStock));

        if (availableSellers < 0)
            throw new ArgumentException("Available sellers cannot be negative", nameof(availableSellers));

        return new StockInfo(totalStock, availableSellers, DateTime.UtcNow);
    }

    public static StockInfo Empty() => new StockInfo(0, 0, DateTime.UtcNow);

    public bool IsAvailable() => TotalStock > 0 && AvailableSellers > 0;

    public StockInfo UpdateStock(int totalStock, int availableSellers)
    {
        return Create(totalStock, availableSellers);
    }

    public bool Equals(StockInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return TotalStock == other.TotalStock && AvailableSellers == other.AvailableSellers;
    }

    public override bool Equals(object? obj) => Equals(obj as StockInfo);

    public override int GetHashCode() => HashCode.Combine(TotalStock, AvailableSellers);

    public static bool operator ==(StockInfo? left, StockInfo? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(StockInfo? left, StockInfo? right) => !(left == right);
}

