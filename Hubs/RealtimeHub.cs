using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RealTime.Constants;
using RealTime.Models;
using RealTime.Services;

namespace RealTime.Hubs;

public class RealtimeHub : Hub
{
    private readonly PgNotificationService _pg;
    private readonly ILogger<RealtimeHub> _logger;
    private readonly string _apiKey;

    public RealtimeHub(PgNotificationService pg, ILogger<RealtimeHub> logger, IConfiguration configuration)
    {
        _pg = pg;
        _logger = logger;
        _apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey is not configured.");
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var providedKey = httpContext?.Request.Query["apiKey"].ToString();

        if (providedKey != _apiKey)
        {
            _logger.LogWarning("Rejected connection {ConnectionId}: invalid or missing API key.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
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
                await Clients.Caller.SendAsync(ClientMethods.ReceiveNotification, notification);

            Console.WriteLine($"Sent stored notifications to caller for {groupName}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB unavailable, cannot load stored notifications for {Group}.", groupName);
        }
    }

    public async Task SendToFrontend(string groupName, JsonElement payload)
    {
        Console.WriteLine($"[SEND TO FRONTEND KEY]: {groupName}");
        await Clients.Group(groupName).SendAsync(ClientMethods.ReceiveNotification, payload);
    }

    public async Task SendToFrontendGlobal(string groupName, JsonElement payload)
    {
        Console.WriteLine($"[SEND TO FRONTEND GLOBAL KEY]: {groupName}");
        await Clients.Group(groupName).SendAsync(ClientMethods.ReceiveNotification, payload);
    }

    public async Task NotifyDelayBus(string groupName, JsonElement payload)
    {
        Console.WriteLine($"[SEND TO NOTIFY DELAY BUS]: {groupName}");
        await Clients.Group(groupName).SendAsync(ClientMethods.ReceiveNotification, payload);
    }

    public async Task NotifyAdminFromCamera(string groupName, JsonElement payload)
    {
        Console.WriteLine($"[NOTIFY ADMIN FROM CAMERA KEY]: {groupName}");
        await Clients.Group(groupName).SendAsync(ClientMethods.ReceiveNotification, payload);
    }

    public async Task DeleteNotification(string notificationId, string groupName)
    {
        Console.WriteLine($"[DELETE NOTIFICATION] Deleting {notificationId} from {groupName}");
        await Clients.Group(groupName).SendAsync(ClientMethods.DeleteNotification, notificationId);
    }
}
