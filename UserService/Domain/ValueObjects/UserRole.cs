namespace UserService.Domain.ValueObjects;

/// <summary>
/// Enum representing user roles with permission levels
/// </summary>
public enum UserRole
{
    Student = 0,
    Seller = 1,
    Admin = 2
}

/// <summary>
/// Extension methods for UserRole
/// </summary>
public static class UserRoleExtensions
{
    /// <summary>
    /// Checks if the role has permission to perform admin actions
    /// </summary>
    public static bool CanPerformAdminActions(this UserRole role)
    {
        return role == UserRole.Admin;
    }

    /// <summary>
    /// Checks if the role can sell items
    /// </summary>
    public static bool CanSellItems(this UserRole role)
    {
        return role == UserRole.Seller || role == UserRole.Admin;
    }

    /// <summary>
    /// Checks if the role can purchase items
    /// </summary>
    public static bool CanPurchaseItems(this UserRole role)
    {
        return true; // All roles can purchase
    }

    /// <summary>
    /// Gets the display name for the role
    /// </summary>
    public static string GetDisplayName(this UserRole role)
    {
        return role switch
        {
            UserRole.Student => "Student",
            UserRole.Seller => "Seller",
            UserRole.Admin => "Administrator",
            _ => role.ToString()
        };
    }

    /// <summary>
    /// Validates if a role string is valid
    /// </summary>
    public static bool IsValidRole(string role)
    {
        return Enum.TryParse<UserRole>(role, true, out _);
    }

    /// <summary>
    /// Parses a role string to UserRole enum
    /// </summary>
    public static UserRole ParseRole(string role)
    {
        if (Enum.TryParse<UserRole>(role, true, out var result))
        {
            return result;
        }
        return UserRole.Student; // Default
    }
}

