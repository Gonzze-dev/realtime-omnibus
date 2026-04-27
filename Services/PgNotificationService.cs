using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealTime.Data;
using RealTime.Models;

namespace RealTime.Services;

public class PgNotificationService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task UpdateGroupAsync(string notifId, string? groupKey, string groupName)
    {
        var id = Guid.Parse(notifId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var n = await db.Notifications.FindAsync(id);
        if (n is null)
        {
            Console.WriteLine($"[UPDATE GROUP] Notification {id} not found.");
            return;
        }
        n.GroupKey = groupKey;
        n.GroupName = groupName;
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<JsonElement>> GetByGroupAsync(string groupName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var payloads = await db.Notifications
            .Where(n => n.GroupName == groupName && n.Expiration > DateTime.UtcNow)
            .Select(n => n.Payload)
            .ToListAsync();

        return payloads.Select(p => JsonSerializer.Deserialize<JsonElement>(p));
    }

    public async Task DeleteByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var notification = await db.Notifications.FindAsync(id);
        if (notification is null)
        {
            Console.WriteLine($"[DELETE] Notification {id} not found in database.");
            return;
        }

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DELETE] Notification {id} removed from {notification.GroupName}.");
    }

    public async Task<string?> GetGroupNameByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Notifications
            .Where(n => n.Id == id)
            .Select(n => n.GroupName)
            .FirstOrDefaultAsync();
    }
}
