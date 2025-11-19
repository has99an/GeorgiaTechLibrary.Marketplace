using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for AuthUser entity
/// </summary>
public class AuthUserRepository : IAuthUserRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthUserRepository> _logger;

    public AuthUserRepository(AppDbContext context, ILogger<AuthUserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuthUser?> GetAuthUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.AuthUsers
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<AuthUser?> GetAuthUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.AuthUsers
            .FirstOrDefaultAsync(u => u.Email.Value == email.ToLower(), cancellationToken);
    }

    public async Task<AuthUser> AddAuthUserAsync(AuthUser authUser, CancellationToken cancellationToken = default)
    {
        await _context.AuthUsers.AddAsync(authUser, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return authUser;
    }

    public async Task<AuthUser> UpdateAuthUserAsync(AuthUser authUser, CancellationToken cancellationToken = default)
    {
        _context.AuthUsers.Update(authUser);
        await _context.SaveChangesAsync(cancellationToken);
        return authUser;
    }

    public async Task<bool> AuthUserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.AuthUsers.AnyAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.AuthUsers.AnyAsync(u => u.Email.Value == email.ToLower(), cancellationToken);
    }
}

