using AuthService.Domain.Exceptions;
using AuthService.Domain.ValueObjects;

namespace AuthService.Domain.Entities;

/// <summary>
/// Rich domain entity representing an authenticated user
/// </summary>
public class AuthUser
{
    public Guid UserId { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? LastLoginDate { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndDate { get; private set; }

    // Private constructor for EF Core
    private AuthUser()
    {
        Email = null!;
        PasswordHash = string.Empty;
    }

    private AuthUser(Guid userId, Email email, string passwordHash, DateTime createdDate)
    {
        UserId = userId;
        Email = email;
        PasswordHash = passwordHash;
        CreatedDate = createdDate;
        FailedLoginAttempts = 0;
    }

    /// <summary>
    /// Factory method to create a new AuthUser
    /// </summary>
    public static AuthUser Create(string email, string passwordHash)
    {
        var emailVO = Email.Create(email);
        
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new AuthenticationException("Password hash is required");
        }

        return new AuthUser(
            Guid.NewGuid(),
            emailVO,
            passwordHash,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Factory method to create an AuthUser with existing ID (for seeding/reconstruction)
    /// </summary>
    public static AuthUser CreateWithId(Guid userId, string email, string passwordHash, DateTime createdDate)
    {
        var emailVO = Email.Create(email);
        
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new AuthenticationException("Password hash is required");
        }

        return new AuthUser(userId, emailVO, passwordHash, createdDate);
    }

    /// <summary>
    /// Records a successful login
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginDate = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndDate = null;
    }

    /// <summary>
    /// Records a failed login attempt
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        
        // Lock account after 5 failed attempts
        if (FailedLoginAttempts >= 5)
        {
            LockoutEndDate = DateTime.UtcNow.AddMinutes(15);
        }
    }

    /// <summary>
    /// Checks if the account is locked out
    /// </summary>
    public bool IsLockedOut()
    {
        if (LockoutEndDate == null)
            return false;

        if (LockoutEndDate.Value > DateTime.UtcNow)
            return true;

        // Lockout period expired, reset
        LockoutEndDate = null;
        FailedLoginAttempts = 0;
        return false;
    }

    /// <summary>
    /// Updates the password hash
    /// </summary>
    public void UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new AuthenticationException("Password hash cannot be empty");
        }

        PasswordHash = newPasswordHash;
    }

    /// <summary>
    /// Gets the email value as string (for persistence)
    /// </summary>
    public string GetEmailString() => Email.Value;

    /// <summary>
    /// Gets masked email for logging
    /// </summary>
    public string GetMaskedEmail() => Email.GetMaskedValue();
}

