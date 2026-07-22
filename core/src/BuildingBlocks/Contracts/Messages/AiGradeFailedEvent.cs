namespace Contracts.Messages;

/// <summary>
/// Published by the Python AIGradingService when grading a paper failed unrecoverably, and consumed
/// by GradingService's AiGradeFailedConsumer which moves AiStatus to Failed. Idempotent by
/// <see cref="MessageId"/> (MassTransit inbox).
/// </summary>
public record AiGradeFailedEvent(
    Guid MessageId,
    Guid AssignmentId,
    string Reason,
    Guid? CorrelationId,
    DateTime OccurredAt
);
