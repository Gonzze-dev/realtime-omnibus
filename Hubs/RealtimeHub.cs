using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RealTime.Services;

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
        uint timeLifeSeconds = 60;

        string serialized = JsonSerializer.Serialize(payload);

        Console.WriteLine($"Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            if (timeLifeSeconds > 0)
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(timeLifeSeconds));
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
        Console.WriteLine($"Sending to {groupName}: {payload}");
        await Clients.Group(groupName).SendAsync(methodName, payload);
    }

    public async Task NotifyDelayBus(string groupKey, JsonElement payload)
    {
        string groupName = $"frontend/{groupKey}";
        string methodName = "receiveNotification";
        string redisKey = $"notifications:{groupName}";
        uint timeLifeSeconds = 60;

        string serialized = JsonSerializer.Serialize(payload);

        Console.WriteLine($"[DELAY BUS] Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            if (timeLifeSeconds > 0)
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(timeLifeSeconds));
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
        uint timeLifeSeconds = 60;

        string serialized = JsonSerializer.Serialize(payload);

        Console.WriteLine($"[ADMIN FROM CAMERA] Sending to {groupName}: {payload}");

        try
        {
            var db = _redis.GetDatabase();
            await db.ListRightPushAsync(redisKey, serialized);
            if (timeLifeSeconds > 0)
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(timeLifeSeconds));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync(methodName, payload);
    }
}
