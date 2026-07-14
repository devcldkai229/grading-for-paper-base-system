using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Consumers;

public class ExportReadyConsumer : IConsumer<ExportReadyEvent>
{
    private readonly INotificationDispatchService _dispatch;
    private readonly NotificationIdempotencyGuard _idempotency;
    private readonly ILogger<ExportReadyConsumer> _logger;

    public ExportReadyConsumer(
        INotificationDispatchService dispatch,
        NotificationIdempotencyGuard idempotency,
        ILogger<ExportReadyConsumer> logger)
    {
        _dispatch = dispatch;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExportReadyEvent> context)
    {
        var evt = context.Message;
        var dedupeKey = $"notification:export-ready:{evt.MessageId}";

        if (!await _idempotency.TryClaimAsync(dedupeKey))
        {
            _logger.LogInformation("ExportReadyEvent skipped (duplicate): MessageId={MessageId}", evt.MessageId);
            return;
        }

        try
        {
            await _dispatch.HandleExportReadyAsync(evt, context.CancellationToken);
        }
        catch
        {
            await _idempotency.ReleaseAsync(dedupeKey);
            throw;
        }
    }
}
