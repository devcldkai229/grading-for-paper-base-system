namespace Contracts.Messages;

/// <summary>
/// Published when an Admin allocates or reassigns an alias range to a lecturer (marker
/// assignment). Consumed by NotificationService to notify the lecturer they have new papers
/// to grade.
/// </summary>
public record AssignmentNotificationEvent(
    Guid MessageId,
    Guid TeacherId,
    Guid SubjectId,
    int AliasStart,
    int AliasEnd,
    DateTime OccurredAt,
    Guid? BatchId = null,
    string? ZipFileName = null,
    int? PaperCount = null
);
