namespace Contracts.Messages;

/// <summary>
/// State-replication event published (via transactional outbox) by IamService whenever a lecturer's
/// profile relevant to grading changes (currently the marker code). Consumers keep a local projection
/// so they never have to bulk-pull the whole user list synchronously (removes coupling — N5).
/// </summary>
public record LecturerProfileChanged(
    Guid MessageId,
    Guid UserId,
    string? MarkerCode,
    string? FullName,
    Guid? CorrelationId,
    DateTime OccurredAt
);
