namespace Contracts.Messages;

/// <summary>
/// Published (via GradingService's transactional outbox) when a paper's scores are overridden
/// (re-graded) after the original submission. Carries the same shape as <see cref="ScoreSubmitted"/>;
/// ReportingService applies it to the exact same <c>score_record</c>/<c>feedback_record</c>
/// projections with latest-wins semantics keyed on <see cref="OccurredAt"/>. Kept as a distinct
/// message type purely for clearer semantics/auditing.
/// </summary>
public record ScoreOverridden(
    Guid MessageId,
    Guid AssignmentId,
    Guid SubjectId,
    Guid TeacherId,
    Guid PaperId,
    int? AliasNumber,
    string? StudentAlias,
    decimal TotalScore,
    decimal MaxScore,
    string? PaperComment,
    IReadOnlyList<ScoreQuestionPayload> Questions,
    DateTime OccurredAt
);
