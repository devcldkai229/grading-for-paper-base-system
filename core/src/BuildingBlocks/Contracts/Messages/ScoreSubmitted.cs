namespace Contracts.Messages;

/// <summary>
/// Published (via GradingService's transactional outbox) when a marker submits/finalizes a paper's
/// scores. This is a deliberately "fat" event: it carries both the aggregate score and the full
/// per-question feedback so ReportingService can build BOTH its score-distribution/pass-fail
/// projection (<c>score_record</c>) and its feedback projection (<c>feedback_record</c>) from a
/// single message. Idempotent by <see cref="MessageId"/> and out-of-order safe via <see cref="OccurredAt"/>.
/// </summary>
public record ScoreSubmitted(
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

public record ScoreQuestionPayload(
    string QuestionNumber,
    string? Label,
    decimal Score,
    decimal MaxScore,
    string? QuestionComment
);
