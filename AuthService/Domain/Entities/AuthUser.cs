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
    public string Role { get; private set; } // User role: Student, Seller, or Admin
    public DateTime CreatedDate { get; private set; }
    public DateTime? LastLoginDate { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndDate { get; private set; }

    // Private constructor for EF Core
    private AuthUser()
    {
        Email = null!;
        PasswordHash = string.Empty;
        Role = "Student"; // Default role
    }

    private AuthUser(Guid userId, Email email, string passwordHash, string role, DateTime createdDate)
    {
        UserId = userId;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedDate = createdDate;
        FailedLoginAttempts = 0;
    }

    /// <summary>
    /// Factory method to create a new AuthUser
    /// </summary>
    public static AuthUser Create(string email, string passwordHash, string role = "Student")
    {
        var emailVO = Email.Create(email);
        
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new AuthenticationException("Password hash is required");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            role = "Student"; // Default role
        }

        return new AuthUser(
            Guid.NewGuid(),
            emailVO,
            passwordHash,
            role,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Factory method to create an AuthUser with existing ID (for seeding/reconstruction)
    /// </summary>
    public static AuthUser CreateWithId(Guid userId, string email, string passwordHash, string role, DateTime createdDate)
    {
        var emailVO = Email.Create(email);
        
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new AuthenticationException("Password hash is required");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            role = "Student"; // Default role
        }

        return new AuthUser(userId, emailVO, passwordHash, role, createdDate);
    }

    /// <summary>
    /// Updates the user's role (called when role changes in UserService)
    /// </summary>
    public void UpdateRole(string newRole)
    {
        if (string.IsNullOrWhiteSpace(newRole))
        {
            throw new AuthenticationException("Role cannot be empty");
        }

        // Validate role
        if (newRole != "Student" && newRole != "Seller" && newRole != "Admin")
        {
            throw new AuthenticationException($"Invalid role: {newRole}. Must be Student, Seller, or Admin");
        }

        Role = newRole;
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

