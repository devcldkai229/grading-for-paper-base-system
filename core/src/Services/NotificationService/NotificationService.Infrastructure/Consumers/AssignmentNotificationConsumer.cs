using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Consumers;

public class AssignmentNotificationConsumer : IConsumer<AssignmentNotificationEvent>
{
    private readonly INotificationDispatchService _dispatch;
    private readonly INotificationPushService _push;
    private readonly NotificationIdempotencyGuard _idempotency;
    private readonly ILogger<AssignmentNotificationConsumer> _logger;

    public AssignmentNotificationConsumer(
        INotificationDispatchService dispatch,
        INotificationPushService push,
        NotificationIdempotencyGuard idempotency,
        ILogger<AssignmentNotificationConsumer> logger)
    {
        _dispatch = dispatch;
        _idempotency = idempotency;
        _push = push;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AssignmentNotificationEvent> context)
    {
        var evt = context.Message;
        var dedupeKey = $"notification:assignment:{evt.MessageId}";

        if (!await _idempotency.TryClaimAsync(dedupeKey))
        {
            _logger.LogInformation("AssignmentNotificationEvent skipped (duplicate): MessageId={MessageId}", evt.MessageId);
            return;
        }

        try
        {
            await _dispatch.HandleAssignmentAsync(evt, context.CancellationToken);

            var title = evt.BatchId.HasValue
                ? "Bạn vừa được giao folder chấm bài"
                : "Bạn được phân công chấm bài mới";
            var body = evt.BatchId.HasValue
                ? $"{(string.IsNullOrWhiteSpace(evt.ZipFileName) ? "Folder ZIP" : evt.ZipFileName)} — {evt.PaperCount ?? (evt.AliasEnd - evt.AliasStart + 1)} bài"
                : $"Bạn được phân công chấm các bài từ Student_{evt.AliasStart:D4} đến Student_{evt.AliasEnd:D4}.";

            await _push.PushToUserAsync(
                evt.TeacherId,
                new RealtimeNotificationPayload("assignment", title, body, evt.BatchId, evt.SubjectId),
                context.CancellationToken);
        }
        catch
        {
            await _idempotency.ReleaseAsync(dedupeKey);
            throw;
        }
    }
}
