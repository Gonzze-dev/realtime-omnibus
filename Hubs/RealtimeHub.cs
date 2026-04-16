using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RealTime.Services;
using RealTime.Utils;

namespace RealTime.Hubs;

public class RealtimeHub : Hub
{
    private readonly IRedisConnectionService _redis;
    private readonly ILogger<RealtimeHub> _logger;

    public RealtimeHub(IRedisConnectionService redis, ILogger<RealtimeHub> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task JoinFrontendGroup(string licensePatent)
    {
        Console.WriteLine($"Joining frontend {licensePatent}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"frontend/{licensePatent}");
    }

    public async Task LeaveFrontendGroup(string licensePatent)
    {
        Console.WriteLine($"Leaving frontend {licensePatent}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"frontend/{licensePatent}");
    }

    public async Task JoinFrontend(string idGroup)
    {
        string groupName = $"frontend/{idGroup}";
        string redisKey = $"notifications:{groupName}";

        Console.WriteLine($"[JOIN FRONTEND] Joining {groupName}");

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        try
        {
            var db = _redis.GetDatabase();
            var stored = await db.ListRangeAsync(redisKey);

            if (stored.Length <= 0)
            {
                Console.WriteLine($"No stored notifications found for {groupName}");
                return;
            }

            var notifications = stored
                .Select(v => JsonSerializer.Deserialize<JsonElement>((string)v!))
                .Where(e => e.TryGetProperty("data", out _))
                .Select(e => e.GetProperty("data"))
                .ToArray();

            foreach (var notification in notifications)
            {
                await Clients.Caller.SendAsync("receiveNotification", notification);
            }

            Console.WriteLine($"Sent {notifications.Length} stored notifications to caller for {groupName}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, cannot load stored notifications for {Group}.", groupName);
        }
    }

    public async Task SendToFrontend(string groupKey, JsonElement payload)
    {
        string groupName = $"frontend/{groupKey}";
        string methodName = "receiveNotification";
        string redisKey = $"notifications:{groupName}";
        var timeLife = PayloadUtils.ExtractTimeLife(payload);

        string serialized = JsonSerializer.Serialize(new { groupName, data = payload });

        Console.WriteLine($"Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            await db.KeyExpireAsync(redisKey, timeLife);
            await RedisNotificationUtils.StoreNotificationIndexAsync(db, payload, groupName, timeLife);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync(methodName, payload);
    }

    public async Task SendToFrontendGlobal(JsonElement payload)
    {
        string methodName = "receiveNotification";
        string groupName = "frontend/global";
        string redisKey = $"notifications:{groupName}";
        var timeLife = PayloadUtils.ExtractTimeLife(payload);

        string serialized = JsonSerializer.Serialize(new { groupName, data = payload });

        Console.WriteLine($"Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            await db.KeyExpireAsync(redisKey, timeLife);
            await RedisNotificationUtils.StoreNotificationIndexAsync(db, payload, groupName, timeLife);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync(methodName, payload);
    }

    public async Task NotifyDelayBus(string groupKey, JsonElement payload)
    {
        string groupName = $"frontend/{groupKey}";
        string methodName = "receiveNotification";
        string redisKey = $"notifications:{groupName}";
        var timeLife = PayloadUtils.ExtractTimeLife(payload);

        string serialized = JsonSerializer.Serialize(new { groupName, data = payload });

        Console.WriteLine($"[DELAY BUS] Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            await db.KeyExpireAsync(redisKey, timeLife);
            await RedisNotificationUtils.StoreNotificationIndexAsync(db, payload, groupName, timeLife);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync(methodName, payload);
    }

    public async Task NotifyAdminFromCamera(string groupKey, JsonElement payload)
    {
        string groupName = $"frontend/admin/{groupKey}";
        string methodName = "receiveNotification";
        string redisKey = $"notifications:{groupName}";
        var timeLife = PayloadUtils.ExtractTimeLife(payload);

        string serialized = JsonSerializer.Serialize(new { groupName, data = payload });

        Console.WriteLine($"[ADMIN FROM CAMERA] Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            await db.KeyExpireAsync(redisKey, timeLife);
            await RedisNotificationUtils.StoreNotificationIndexAsync(db, payload, groupName, timeLife);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync(methodName, payload);
    }

    public async Task DeleteNotification(string notificationId)
    {
        string indexKey = $"notification-index:{notificationId}";

        try
        {
            var db = _redis.GetDatabase();

            var groupName = (string?)await db.StringGetAsync(indexKey);
            if (groupName is null)
            {
                Console.WriteLine($"[DELETE] Notification {notificationId} not found in index.");
                return;
            }

            string redisKey = $"notifications:{groupName}";

            var entries = await db.ListRangeAsync(redisKey);
            foreach (var entry in entries)
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>((string)entry!);
                if (parsed.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("payload", out var innerPayload) &&
                    innerPayload.TryGetProperty("id", out var idProp) &&
                    idProp.GetString() == notificationId)
                {
                    await db.ListRemoveAsync(redisKey, entry, 1);
                    break;
                }
            }

            await db.KeyDeleteAsync(indexKey);

            Console.WriteLine($"[DELETE] Notification {notificationId} removed from {groupName}.");

            await Clients.Group(groupName).SendAsync("deleteNotification", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, could not delete notification {Id}.", notificationId);
        }
    }

}
