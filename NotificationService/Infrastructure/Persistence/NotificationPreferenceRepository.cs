using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly AppDbContext _context;

    public NotificationPreferenceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(string userId)
    {
        return await _context.NotificationPreferences
            .FirstOrDefaultAsync(np => np.UserId == userId);
    }

    public async Task<NotificationPreference> CreateAsync(NotificationPreference preference)
    {
        _context.NotificationPreferences.Add(preference);
        await _context.SaveChangesAsync();
        return preference;
    }

    public async Task UpdateAsync(NotificationPreference preference)
    {
        _context.NotificationPreferences.Update(preference);
        await _context.SaveChangesAsync();
    }

    public async Task<NotificationPreference> GetOrCreateForUserAsync(string userId)
    {
        var preference = await GetByUserIdAsync(userId);
        
        if (preference == null)
        {
            preference = NotificationPreference.CreateDefault(userId);
            await CreateAsync(preference);
        }

        return preference;
    }
}

