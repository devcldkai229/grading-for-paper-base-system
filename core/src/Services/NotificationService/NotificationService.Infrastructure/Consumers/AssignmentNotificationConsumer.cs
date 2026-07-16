using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Consumers;

public class AssignmentNotificationConsumer : IConsumer<AssignmentNotificationEvent>
{
    private readonly INotificationDispatchService _dispatch;
    private readonly NotificationIdempotencyGuard _idempotency;
    private readonly ILogger<AssignmentNotificationConsumer> _logger;

    public AssignmentNotificationConsumer(
        INotificationDispatchService dispatch,
        NotificationIdempotencyGuard idempotency,
        ILogger<AssignmentNotificationConsumer> logger)
    {
        _dispatch = dispatch;
        _idempotency = idempotency;
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
        }
        catch
        {
            await _idempotency.ReleaseAsync(dedupeKey);
            throw;
        }
    }
}
