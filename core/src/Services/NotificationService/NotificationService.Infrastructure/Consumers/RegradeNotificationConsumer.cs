using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Consumers;

public class RegradeNotificationConsumer : IConsumer<RegradeNotificationEvent>
{
    private readonly INotificationDispatchService _dispatch;
    private readonly NotificationIdempotencyGuard _idempotency;
    private readonly ILogger<RegradeNotificationConsumer> _logger;

    public RegradeNotificationConsumer(
        INotificationDispatchService dispatch,
        NotificationIdempotencyGuard idempotency,
        ILogger<RegradeNotificationConsumer> logger)
    {
        _dispatch = dispatch;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RegradeNotificationEvent> context)
    {
        var evt = context.Message;
        var dedupeKey = $"notification:regrade:{evt.MessageId}";

        if (!await _idempotency.TryClaimAsync(dedupeKey))
        {
            _logger.LogInformation("RegradeNotificationEvent skipped (duplicate): MessageId={MessageId}", evt.MessageId);
            return;
        }

        try
        {
            await _dispatch.HandleRegradeAsync(evt, context.CancellationToken);
        }
        catch
        {
            await _idempotency.ReleaseAsync(dedupeKey);
            throw;
        }
    }
}
