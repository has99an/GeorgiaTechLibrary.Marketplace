using UserService.Models;

namespace UserService.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User> AddUserAsync(User user);
    Task<User?> UpdateUserAsync(Guid userId, User user);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<bool> UserExistsAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
}
