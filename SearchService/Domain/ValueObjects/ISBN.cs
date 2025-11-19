namespace SearchService.Domain.ValueObjects;

/// <summary>
/// Value object representing an ISBN (International Standard Book Number)
/// </summary>
public sealed class ISBN : IEquatable<ISBN>
{
    public string Value { get; }

    private ISBN(string value)
    {
        Value = value;
    }

    public static ISBN Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ISBN cannot be empty", nameof(value));

        // Remove hyphens and spaces for validation
        var cleanValue = value.Replace("-", "").Replace(" ", "");

        if (cleanValue.Length != 10 && cleanValue.Length != 13)
            throw new ArgumentException("ISBN must be 10 or 13 characters", nameof(value));

        if (!cleanValue.All(char.IsDigit))
            throw new ArgumentException("ISBN must contain only digits", nameof(value));

        return new ISBN(value);
    }

    public static ISBN? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return Create(value);
        }
        catch
        {
            return null;
        }
    }

    public bool Equals(ISBN? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as ISBN);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(ISBN? left, ISBN? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(ISBN? left, ISBN? right) => !(left == right);

    public static implicit operator string(ISBN isbn) => isbn.Value;
}

