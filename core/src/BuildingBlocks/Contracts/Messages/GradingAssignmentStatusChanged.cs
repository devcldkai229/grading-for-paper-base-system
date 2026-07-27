namespace Contracts.Messages;

/// <summary>
/// Published (via GradingService's transactional outbox) whenever a grading assignment's lifecycle
/// status changes (e.g. NotStarted -> Drafting -> Submitted). ReportingService consumes this to keep
/// its local <c>subject_progress</c>/<c>marker_progress</c> projections up to date without calling
/// GradingService synchronously. Idempotent by <see cref="MessageId"/> (MassTransit inbox) and
/// out-of-order safe via <see cref="OccurredAt"/>.
/// </summary>
public record GradingAssignmentStatusChanged(
    Guid MessageId,
    Guid AssignmentId,
    Guid SubjectId,
    Guid TeacherId,
    Guid PaperId,
    int? AliasNumber,
    string Status,
    DateTime OccurredAt
);
