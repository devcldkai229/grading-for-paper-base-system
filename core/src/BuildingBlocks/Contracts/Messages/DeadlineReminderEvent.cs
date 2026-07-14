namespace Contracts.Messages;

/// <summary>
/// Published when a subject's exam deadline is approaching and a lecturer still has ungraded
/// papers. Consumed by NotificationService to remind the lecturer.
/// </summary>
public record DeadlineReminderEvent(
    Guid MessageId,
    Guid TeacherId,
    Guid SubjectId,
    string SubjectCode,
    DateOnly ExamEndDate,
    int RemainingCount,
    DateTime OccurredAt
);
