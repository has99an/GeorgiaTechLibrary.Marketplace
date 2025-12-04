using System.Text.RegularExpressions;

namespace OrderService.Domain.ValueObjects;

/// <summary>
/// Value object representing a delivery address with validation
/// </summary>
public sealed class Address : IEquatable<Address>
{
    private static readonly Regex PostalCodeRegex = new(
        @"^\d{4}$",
        RegexOptions.Compiled);

    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string? State { get; }
    public string? Country { get; }

    private Address(string street, string city, string postalCode, string? state = null, string? country = null)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        State = state;
        Country = country;
    }

    /// <summary>
    /// Creates a new Address value object with validation
    /// </summary>
    public static Address Create(string street, string city, string postalCode, string? state = null, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ArgumentException("Street address is required", nameof(street));
        }

        var trimmedStreet = street.Trim();
        if (trimmedStreet.Length > 200)
        {
            throw new ArgumentException("Street address cannot exceed 200 characters", nameof(street));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City is required", nameof(city));
        }

        var trimmedCity = city.Trim();
        if (trimmedCity.Length > 100)
        {
            throw new ArgumentException("City cannot exceed 100 characters", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ArgumentException("Postal code is required", nameof(postalCode));
        }

        var trimmedPostalCode = postalCode.Trim();
        if (!PostalCodeRegex.IsMatch(trimmedPostalCode))
        {
            throw new ArgumentException("Postal code must be 4 digits", nameof(postalCode));
        }

        var trimmedState = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        if (trimmedState != null && trimmedState.Length > 100)
        {
            throw new ArgumentException("State cannot exceed 100 characters", nameof(state));
        }

        var trimmedCountry = string.IsNullOrWhiteSpace(country) ? "Denmark" : country.Trim();
        if (trimmedCountry.Length > 100)
        {
            throw new ArgumentException("Country cannot exceed 100 characters", nameof(country));
        }

        return new Address(trimmedStreet, trimmedCity, trimmedPostalCode, trimmedState, trimmedCountry);
    }

    /// <summary>
    /// Gets the full address as a formatted string
    /// </summary>
    public string GetFullAddress()
    {
        var address = $"{Street}, {PostalCode} {City}";
        if (!string.IsNullOrWhiteSpace(State))
        {
            address += $", {State}";
        }
        if (!string.IsNullOrWhiteSpace(Country))
        {
            address += $", {Country}";
        }
        return address;
    }

    public override string ToString() => GetFullAddress();

    public bool Equals(Address? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Street == other.Street &&
               City == other.City &&
               PostalCode == other.PostalCode &&
               State == other.State &&
               Country == other.Country;
    }

    public override bool Equals(object? obj) => obj is Address address && Equals(address);

    public override int GetHashCode() => HashCode.Combine(Street, City, PostalCode, State, Country);

    public static bool operator ==(Address? left, Address? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Address? left, Address? right) => !(left == right);
}

