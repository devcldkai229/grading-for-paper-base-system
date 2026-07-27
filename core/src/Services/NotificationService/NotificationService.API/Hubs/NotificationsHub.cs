using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.API.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("UserId")?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        return base.OnConnectedAsync();
    }

    public static string UserGroup(string userId) => $"user:{userId}";
}
