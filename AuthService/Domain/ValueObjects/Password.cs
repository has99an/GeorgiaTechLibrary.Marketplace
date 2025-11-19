using System.Text.RegularExpressions;
using AuthService.Domain.Exceptions;

namespace AuthService.Domain.ValueObjects;

/// <summary>
/// Value object representing a password with validation rules
/// </summary>
public sealed class Password
{
    private static readonly Regex UpperCaseRegex = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowerCaseRegex = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"[0-9]", RegexOptions.Compiled);
    private static readonly Regex SpecialCharRegex = new(@"[!@#$%^&*(),.?""':{}|<>]", RegexOptions.Compiled);

    public string Value { get; }

    private Password(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Password value object with validation
    /// </summary>
    public static Password Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new AuthenticationException("Password is required");
        }

        if (password.Length < 8)
        {
            throw new AuthenticationException("Password must be at least 8 characters long");
        }

        if (password.Length > 100)
        {
            throw new AuthenticationException("Password cannot exceed 100 characters");
        }

        // Check password complexity
        var hasUpper = UpperCaseRegex.IsMatch(password);
        var hasLower = LowerCaseRegex.IsMatch(password);
        var hasDigit = DigitRegex.IsMatch(password);
        var hasSpecial = SpecialCharRegex.IsMatch(password);

        var complexityCount = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);

        if (complexityCount < 3)
        {
            throw new AuthenticationException(
                "Password must contain at least 3 of the following: uppercase letter, lowercase letter, digit, special character");
        }

        return new Password(password);
    }

    /// <summary>
    /// Creates a password without validation (for loading from database)
    /// </summary>
    public static Password CreateWithoutValidation(string password)
    {
        return new Password(password);
    }

    public override string ToString() => "***REDACTED***";
}

