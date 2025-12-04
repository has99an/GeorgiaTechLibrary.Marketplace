using UserService.Domain.Exceptions;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Entities;

/// <summary>
/// Rich domain entity representing a User with business logic
/// </summary>
public class User
{
    public Guid UserId { get; private set; }
    public Email Email { get; private set; }
    public string Name { get; private set; }
    public UserRole Role { get; private set; }
    public Address? DeliveryAddress { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? UpdatedDate { get; private set; }
    public bool IsDeleted { get; private set; }

    // Private constructor for EF Core
    private User()
    {
        Email = null!;
        Name = string.Empty;
    }

    private User(Guid userId, Email email, string name, UserRole role, DateTime createdDate)
    {
        UserId = userId;
        Email = email;
        Name = name;
        Role = role;
        CreatedDate = createdDate;
        IsDeleted = false;
    }

    /// <summary>
    /// Factory method to create a new User with validation
    /// </summary>
    public static User Create(string email, string name, UserRole role = UserRole.Student)
    {
        var emailVO = Email.Create(email);
        ValidateName(name);

        return new User(
            Guid.NewGuid(),
            emailVO,
            name.Trim(),
            role,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Factory method to create a User with existing ID (for seeding/reconstruction)
    /// </summary>
    public static User CreateWithId(Guid userId, string email, string name, UserRole role, DateTime createdDate)
    {
        var emailVO = Email.Create(email);
        ValidateName(name);

        return new User(userId, emailVO, name.Trim(), role, createdDate);
    }

    /// <summary>
    /// Updates the user's profile information
    /// </summary>
    public void UpdateProfile(string? name = null, string? email = null, Address? address = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = Email.Create(email);
        }

        if (address != null)
        {
            DeliveryAddress = address;
        }

        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Changes the user's role (admin action)
    /// </summary>
    public void ChangeRole(UserRole newRole)
    {
        if (Role == newRole)
        {
            return; // No change needed
        }

        Role = newRole;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the user has a specific role
    /// </summary>
    public bool IsInRole(UserRole role)
    {
        return Role == role;
    }

    /// <summary>
    /// Checks if the user has admin privileges
    /// </summary>
    public bool IsAdmin()
    {
        return Role == UserRole.Admin;
    }

    /// <summary>
    /// Checks if the user can sell items
    /// </summary>
    public bool CanSell()
    {
        return Role.CanSellItems();
    }

    /// <summary>
    /// Soft deletes the user
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Anonymizes user data for GDPR compliance
    /// </summary>
    public void Anonymize()
    {
        Email = Email.Create($"deleted-{UserId}@anonymized.local");
        Name = "[Deleted User]";
        DeliveryAddress = null;
        IsDeleted = true;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates the name field
    /// </summary>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Name", "Name is required");
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length < 2)
        {
            throw new ValidationException("Name", "Name must be at least 2 characters long");
        }

        if (trimmedName.Length > 200)
        {
            throw new ValidationException("Name", "Name cannot exceed 200 characters");
        }
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

