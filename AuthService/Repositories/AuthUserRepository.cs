using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

public class AuthUserRepository : IAuthUserRepository
{
    private readonly AppDbContext _context;

    public AuthUserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuthUser?> GetAuthUserByIdAsync(Guid userId)
    {
        return await _context.AuthUsers.FindAsync(userId);
    }

    public async Task<AuthUser?> GetAuthUserByEmailAsync(string email)
    {
        return await _context.AuthUsers.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<AuthUser> AddAuthUserAsync(AuthUser authUser)
    {
        _context.AuthUsers.Add(authUser);
        await _context.SaveChangesAsync();
        return authUser;
    }

    public async Task<bool> AuthUserExistsAsync(Guid userId)
    {
        return await _context.AuthUsers.AnyAsync(u => u.UserId == userId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.AuthUsers.AnyAsync(u => u.Email == email);
    }
}
