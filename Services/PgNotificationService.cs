using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealTime.Data;
using RealTime.Models;

namespace RealTime.Services;

public class PgNotificationService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task InsertAsync(Guid id, string? groupKey, string groupName, TimeSpan timeLife, JsonElement payload)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        db.Notifications.Add(new Notification
        {
            Id = id,
            GroupKey = groupKey,
            GroupName = groupName,
            Expiration = DateTime.UtcNow.Add(timeLife),
            Date = DateTime.UtcNow,
            Payload = payload.GetRawText()
        });

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
