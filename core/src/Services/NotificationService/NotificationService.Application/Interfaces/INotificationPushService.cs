namespace NotificationService.Application.Interfaces;

public sealed record RealtimeNotificationPayload(
    string Type,
    string Title,
    string? Body,
    Guid? BatchId,
    Guid? SubjectId);

public interface INotificationPushService
{
    Task PushToUserAsync(Guid userId, RealtimeNotificationPayload payload, CancellationToken ct = default);
}
