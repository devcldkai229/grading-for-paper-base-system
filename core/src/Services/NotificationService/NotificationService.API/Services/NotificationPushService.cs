using Microsoft.AspNetCore.SignalR;
using NotificationService.API.Hubs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Services;

public sealed class NotificationPushService : INotificationPushService
{
    private readonly IHubContext<NotificationsHub> _hub;

    public NotificationPushService(IHubContext<NotificationsHub> hub)
    {
        _hub = hub;
    }

    public Task PushToUserAsync(Guid userId, RealtimeNotificationPayload payload, CancellationToken ct = default) =>
        _hub.Clients
            .Group(NotificationsHub.UserGroup(userId.ToString("D")))
            .SendAsync("ReceiveNotification", payload, ct);
}
