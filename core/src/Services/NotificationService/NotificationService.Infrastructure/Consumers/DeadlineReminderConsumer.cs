using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Consumers;

public class DeadlineReminderConsumer : IConsumer<DeadlineReminderEvent>
{
    private readonly INotificationDispatchService _dispatch;
    private readonly NotificationIdempotencyGuard _idempotency;
    private readonly ILogger<DeadlineReminderConsumer> _logger;

    public DeadlineReminderConsumer(
        INotificationDispatchService dispatch,
        NotificationIdempotencyGuard idempotency,
        ILogger<DeadlineReminderConsumer> logger)
    {
        _dispatch = dispatch;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeadlineReminderEvent> context)
    {
        var evt = context.Message;
        var dedupeKey = $"notification:deadline:{evt.MessageId}";

        if (!await _idempotency.TryClaimAsync(dedupeKey))
        {
            _logger.LogInformation("DeadlineReminderEvent skipped (duplicate): MessageId={MessageId}", evt.MessageId);
            return;
        }

        try
        {
            await _dispatch.HandleDeadlineReminderAsync(evt, context.CancellationToken);
        }
        catch
        {
            await _idempotency.ReleaseAsync(dedupeKey);
            throw;
        }
    }
}
