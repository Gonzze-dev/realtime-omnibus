using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RealTime.Models;
using RealTime.Services;
using RealTime.Utils;

namespace RealTime.Hubs;

public class RealtimeHub : Hub
{
    private readonly PgNotificationService _pg;
    private readonly ILogger<RealtimeHub> _logger;

    public RealtimeHub(PgNotificationService pg, ILogger<RealtimeHub> logger)
    {
        _pg = pg;
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
        Console.WriteLine($"[JOIN FRONTEND] Joining {groupName}");

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        try
        {
            var notifications = await _pg.GetByGroupAsync(groupName);
            foreach (var notification in notifications)
                await Clients.Caller.SendAsync("receiveNotification", notification);

            Console.WriteLine($"Sent stored notifications to caller for {groupName}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, cannot load stored notifications for {Group}.", groupName);
        }
    }

    public async Task SendToFrontend(string groupKey, JsonElement payload)
    {
        Console.WriteLine($"[SEND TO FRONTEND KEY]: {groupKey}");

        string groupName = $"frontend/{groupKey}";
        var timeLife = PayloadUtils.ExtractTimeLife(payload);
        var id = ExtractId(payload);

        try
        {
            await _pg.InsertAsync(id, groupKey, groupName, timeLife, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync("receiveNotification", payload);
    }

    public async Task SendToFrontendGlobal(JsonElement payload)
    {
        string groupName = "frontend/global";
        Console.WriteLine($"[SEND TO FRONTEND GLOBAL KEY]: {groupName}");

        var timeLife = PayloadUtils.ExtractTimeLife(payload);
        var id = ExtractId(payload);

        try
        {
            await _pg.InsertAsync(id, null, groupName, timeLife, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync("receiveNotification", payload);
    }

    public async Task NotifyDelayBus(GroupKeys groupKeys, JsonElement payload)
    {
        string groupName = $"frontend/{groupKeys.Key}";
        Console.WriteLine($"[SEND TO NOTIFY DELAY BUS]: {groupKeys}");

        var timeLife = PayloadUtils.ExtractTimeLife(payload);
        var id = ExtractId(payload);
        try
        {
            await _pg.InsertAsync(id, groupKeys.TerminalID, groupName, timeLife, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync("receiveNotification", payload);
    }

    public async Task NotifyAdminFromCamera(string groupKey, JsonElement payload)
    {
        string groupName = $"frontend/admin/{groupKey}";
        Console.WriteLine($"[NOTFY ADMIN FROM CAMERA KEY]: {groupKey}");

        var timeLife = PayloadUtils.ExtractTimeLife(payload);
        var id = ExtractId(payload);

        try
        {
            await _pg.InsertAsync(id, groupKey, groupName, timeLife, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, notification not stored for {Group}.", groupName);
        }

        await Clients.Group(groupName).SendAsync("receiveNotification", payload);
    }

    public async Task DeleteNotification(string notificationId)
    {
        try
        {
            var id = Guid.Parse(notificationId);
            var groupName = await _pg.GetGroupNameByIdAsync(id);

            if (groupName is null)
            {
                Console.WriteLine($"[DELETE] Notification {notificationId} not found.");
                return;
            }

            await _pg.DeleteByIdAsync(id);
            await Clients.Group(groupName).SendAsync("deleteNotification", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, could not delete notification {Id}.", notificationId);
        }
    }

    private static Guid ExtractId(JsonElement payload) =>
        Guid.Parse(payload.GetProperty("payload").GetProperty("id").GetString()!);
}
