using System.Text.RegularExpressions;
using UserService.Domain.Exceptions;

namespace UserService.Domain.ValueObjects;

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
            throw new ValidationException("Street", "Street address is required");
        }

        var trimmedStreet = street.Trim();
        if (trimmedStreet.Length > 200)
        {
            throw new ValidationException("Street", "Street address cannot exceed 200 characters");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ValidationException("City", "City is required");
        }

        var trimmedCity = city.Trim();
        if (trimmedCity.Length > 100)
        {
            throw new ValidationException("City", "City cannot exceed 100 characters");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ValidationException("PostalCode", "Postal code is required");
        }

        var trimmedPostalCode = postalCode.Trim();
        if (!PostalCodeRegex.IsMatch(trimmedPostalCode))
        {
            throw new ValidationException("PostalCode", "Postal code must be 4 digits");
        }

        var trimmedState = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        if (trimmedState != null && trimmedState.Length > 100)
        {
            throw new ValidationException("State", "State cannot exceed 100 characters");
        }

        var trimmedCountry = string.IsNullOrWhiteSpace(country) ? "Denmark" : country.Trim();
        if (trimmedCountry.Length > 100)
        {
            throw new ValidationException("Country", "Country cannot exceed 100 characters");
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

