using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;

    public NotificationRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> ListByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null) return false;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
