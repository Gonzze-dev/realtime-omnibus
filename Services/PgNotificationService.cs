using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealTime.Data;
using RealTime.Models;

namespace RealTime.Services;

public class PgNotificationService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<IEnumerable<JsonElement>> GetByGroupAsync(string groupName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var payloads = await db.Notifications
            .Where(n => n.GroupName == groupName && n.Expiration > DateTime.UtcNow)
            .Select(n => n.Payload)
            .ToListAsync();

        return payloads.Select(p => JsonSerializer.Deserialize<JsonElement>(p));
    }
}
