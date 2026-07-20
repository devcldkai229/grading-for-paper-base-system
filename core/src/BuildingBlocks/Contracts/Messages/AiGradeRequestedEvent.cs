namespace Contracts.Messages;

/// <summary>
/// Enqueued when a lecturer requests AI grading suggestions for an assignment.
/// Consumed by GradingService's AiGradeConsumer (same service, async worker).
/// </summary>
public record AiGradeRequestedEvent(
    Guid MessageId,
    Guid AssignmentId,
    Guid TeacherId,
    AiGradeSuggestPayload Request,
    DateTime RequestedAt
);

public record AiGradeSuggestPayload(
    Guid AssignmentId,
    Guid SubjectId,
    string RubricVersion,
    IReadOnlyList<AiGradeFileRefPayload> Files,
    IReadOnlyList<AiGradeScoreGridItemPayload> ScoreGrid,
    string? RubricText
);

public record AiGradeFileRefPayload(string Url, string ContentType);

public record AiGradeScoreGridItemPayload(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore
);
