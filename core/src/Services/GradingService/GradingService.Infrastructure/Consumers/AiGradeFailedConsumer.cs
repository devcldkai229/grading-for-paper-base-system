using Contracts.Messages;
using GradingService.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Consumers;

/// <summary>
/// Consumes AI grading failures published by the Python AIGradingService worker and moves AiStatus
/// to Failed — the single writer for that terminal state. Idempotent by MessageId (MassTransit EF
/// inbox) plus the no-op guard inside <see cref="MarkAiGradeFailedAsync"/>.
/// </summary>
public class AiGradeFailedConsumer : IConsumer<AiGradeFailedEvent>
{
    private readonly IGradingSessionService _grading;
    private readonly ILogger<AiGradeFailedConsumer> _logger;

    public AiGradeFailedConsumer(
        IGradingSessionService grading,
        ILogger<AiGradeFailedConsumer> logger)
    {
        _grading = grading;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AiGradeFailedEvent> context)
    {
        var msg = context.Message;

        _logger.LogWarning(
            "AI grade failed: assignment={AssignmentId} message={MessageId} reason={Reason}",
            msg.AssignmentId, msg.MessageId, msg.Reason);

        await _grading.MarkAiGradeFailedAsync(msg.AssignmentId, msg.Reason, context.CancellationToken);
    }
}
