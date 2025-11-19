using System.Text.RegularExpressions;
using UserService.Domain.Exceptions;

namespace UserService.Domain.ValueObjects;

/// <summary>
/// Value object representing an email address with validation
/// </summary>
public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Email value object with validation
    /// </summary>
    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Email", "Email address is required");
        }

        email = email.Trim().ToLowerInvariant();

        if (email.Length > 255)
        {
            throw new ValidationException("Email", "Email address cannot exceed 255 characters");
        }

        if (!EmailRegex.IsMatch(email))
        {
            throw new ValidationException("Email", "Email address format is invalid");
        }

        return new Email(email);
    }

    /// <summary>
    /// Masks the email for logging purposes (e.g., "abc...@example.com")
    /// </summary>
    public string GetMaskedValue()
    {
        var parts = Value.Split('@');
        if (parts.Length != 2)
            return "***@***";

        var localPart = parts[0];
        var domain = parts[1];

        var maskedLocal = localPart.Length <= 3
            ? new string('*', localPart.Length)
            : localPart.Substring(0, 3) + "***";

        return $"{maskedLocal}@{domain}";
    }

    public override string ToString() => Value;

    public bool Equals(Email? other)
    {
        if (other is null) return false;
        return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is Email email && Equals(email);

    public override int GetHashCode() => Value.ToLowerInvariant().GetHashCode();

    public static implicit operator string(Email email) => email.Value;
}

