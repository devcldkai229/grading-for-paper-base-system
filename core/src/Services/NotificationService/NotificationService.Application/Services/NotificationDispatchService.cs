using System.Text.Json;
using Contracts.Messages;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services;

public class NotificationDispatchService : INotificationDispatchService
{
    private readonly INotificationRepository _notifications;

    public NotificationDispatchService(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public Task HandleAssignmentAsync(AssignmentNotificationEvent evt, CancellationToken ct = default)
    {
        if (evt.BatchId.HasValue)
        {
            var label = string.IsNullOrWhiteSpace(evt.ZipFileName) ? "Folder ZIP" : evt.ZipFileName;
            var count = evt.PaperCount ?? (evt.AliasEnd - evt.AliasStart + 1);
            return DispatchAsync(
                evt.TeacherId, NotificationType.Assignment, "Bạn vừa được giao folder chấm bài",
                () => $"{label} — {count} bài",
                evt, ct);
        }

        return DispatchAsync(
            evt.TeacherId, NotificationType.Assignment, "Bạn được phân công chấm bài mới",
            () => $"Bạn được phân công chấm các bài từ Student_{evt.AliasStart:D4} đến Student_{evt.AliasEnd:D4} (SubjectId: {evt.SubjectId}).",
            evt, ct);
    }

    public Task HandleDeadlineReminderAsync(DeadlineReminderEvent evt, CancellationToken ct = default) =>
        DispatchAsync(
            evt.TeacherId, NotificationType.DeadlineReminder, "Sắp đến hạn chấm bài",
            () => $"Môn {evt.SubjectCode} còn {evt.RemainingCount} bài chưa chấm, hạn chót {evt.Deadline:yyyy-MM-dd}.",
            evt, ct);

    public Task HandleExportReadyAsync(ExportReadyEvent evt, CancellationToken ct = default) =>
        DispatchAsync(
            evt.RequestedBy, NotificationType.ExportReady, "File xuất điểm đã sẵn sàng",
            () => $"File xuất điểm cho môn thi (SubjectId: {evt.SubjectId}) đã được tạo thành công.",
            evt, ct);

    public Task HandleRegradeAsync(RegradeNotificationEvent evt, CancellationToken ct = default) =>
        DispatchAsync(
            evt.TeacherId, NotificationType.RegradeRequest, "Bài chấm của bạn đã được điều chỉnh",
            () => $"Điểm bài (AssignmentId: {evt.AssignmentId}) đã được override. Lý do: {evt.Reason}",
            evt, ct);

    /// <summary>
    /// A missing target user id means the event is malformed — nothing to notify. That's recorded
    /// as Failed (for audit/troubleshooting) rather than silently dropped or retried forever.
    /// Every other case reaches the in-app notification store (this system's only delivery
    /// channel today), which is itself the successful "send".
    /// </summary>
    private async Task DispatchAsync<TEvent>(
        Guid userId, NotificationType type, string title, Func<string> buildBody, TEvent evt, CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Metadata = JsonSerializer.Serialize(evt),
        };

        if (userId == Guid.Empty)
        {
            notification.Status = NotificationStatus.Failed;
            notification.Body = "Event is missing a target user id; nothing to notify.";
        }
        else
        {
            notification.Status = NotificationStatus.Sent;
            notification.Body = buildBody();
            notification.SentAt = DateTime.UtcNow;
        }

        await _notifications.AddAsync(notification, ct);
    }
}
