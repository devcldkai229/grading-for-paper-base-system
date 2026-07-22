namespace Contracts.Messages;

/// <summary>
/// Published by the Python AIGradingService when a paper has been graded, and consumed by
/// GradingService's AiGradeCompletedConsumer — the single writer that applies suggestions and
/// moves AiStatus to Completed. Idempotent by <see cref="MessageId"/> (MassTransit inbox).
/// </summary>
public record AiGradeCompletedEvent(
    Guid MessageId,
    Guid AssignmentId,
    IReadOnlyList<AiQuestionGradePayload> QuestionGrades,
    string ModelUsed,
    string PromptVersion,
    Guid? CorrelationId,
    DateTime OccurredAt,
    string? PaperComment = null
);

public record AiQuestionGradePayload(
    string QuestionNumber,
    decimal Score,
    string? QuestionComment,
    double Confidence,
    bool IsManualOnly
);
