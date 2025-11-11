using AuthService.Models;

namespace AuthService.Repositories;

public interface IAuthUserRepository
{
    Task<AuthUser?> GetAuthUserByIdAsync(Guid userId);
    Task<AuthUser?> GetAuthUserByEmailAsync(string email);
    Task<AuthUser> AddAuthUserAsync(AuthUser authUser);
    Task<bool> AuthUserExistsAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
}
