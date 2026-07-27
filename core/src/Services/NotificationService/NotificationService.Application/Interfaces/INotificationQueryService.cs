using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationQueryService
{
    /// <summary>Lists the caller's own notifications, newest first, paginated.</summary>
    Task<NotificationPageDto> GetMyNotificationsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Marks a notification as read. Returns false if it doesn't exist or isn't owned by userId.</summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
}
