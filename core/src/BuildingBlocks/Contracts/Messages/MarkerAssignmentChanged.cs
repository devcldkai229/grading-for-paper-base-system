namespace Contracts.Messages;

/// <summary>
/// State-replication event published (via transactional outbox) by the Allocation capability inside
/// GradingService whenever a marker assignment is created, updated, or deleted. Consumers keep a
/// local read-only projection of "which lecturer marks which subject/alias-range" so they never have
/// to call GradingService synchronously for authorization (removes availability coupling — N5).
/// </summary>
public record MarkerAssignmentChanged(
    Guid MessageId,
    Guid AssignmentId,
    Guid SubjectId,
    Guid TeacherId,
    int AliasStart,
    int AliasEnd,
    MarkerAssignmentChangeType ChangeType,
    Guid? CorrelationId,
    DateTime OccurredAt
);

public enum MarkerAssignmentChangeType
{
    Created,
    Updated,
    Deleted
}
