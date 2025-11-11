using UserService.Data;
using UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace UserService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User> AddUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> UpdateUserAsync(Guid userId, User user)
    {
        var existingUser = await _context.Users.FindAsync(userId);
        if (existingUser == null)
        {
            return null;
        }

        existingUser.Email = user.Email;
        existingUser.Name = user.Name;
        existingUser.Role = user.Role;

        await _context.SaveChangesAsync();
        return existingUser;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UserExistsAsync(Guid userId)
    {
        return await _context.Users.AnyAsync(u => u.UserId == userId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }
}
