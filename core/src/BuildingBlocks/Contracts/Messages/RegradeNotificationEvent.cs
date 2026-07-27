namespace Contracts.Messages;

/// <summary>
/// Published when an assignment's scores are overridden (re-graded) by someone other than its
/// original marker. Consumed by NotificationService to notify the original marker.
/// </summary>
public record RegradeNotificationEvent(
    Guid MessageId,
    Guid TeacherId,
    Guid AssignmentId,
    Guid SubjectId,
    string Reason,
    DateTime OccurredAt
);
