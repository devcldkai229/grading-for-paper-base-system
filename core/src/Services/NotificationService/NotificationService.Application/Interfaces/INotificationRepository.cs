using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Lists a user's own notifications, newest first, paginated.</summary>
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> ListByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Marks a notification as read. Returns false if it doesn't exist or isn't owned by userId.</summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
}
