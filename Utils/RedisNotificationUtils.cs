using System.Text.Json;
using StackExchange.Redis;

namespace RealTime.Utils;

public static class RedisNotificationUtils
{
    public static async Task StoreNotificationIndexAsync(
        IDatabase db,
        JsonElement payload,
        string groupName,
        TimeSpan timeLife)
    {
        if (payload.TryGetProperty("payload", out var innerPayload) &&
            innerPayload.TryGetProperty("id", out var idProp))
        {
            string? notificationId = idProp.GetString();
            if (!string.IsNullOrEmpty(notificationId))
            {
                await db.StringSetAsync($"notification-index:{notificationId}", groupName, timeLife);
            }
        }
    }
}
