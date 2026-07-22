namespace Contracts.Messages;

/// <summary>
/// Enqueued when a lecturer requests AI grading suggestions for an assignment.
/// Published by GradingService (via transactional outbox) and consumed directly by the
/// Python AIGradingService worker, which replies with <see cref="AiGradeCompletedEvent"/>
/// or <see cref="AiGradeFailedEvent"/>.
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
    string? RubricText,
    // Presigned URLs to the ORIGINAL barem file(s) (+ optional exam paper) so the AI reads the real
    // questions / answer keys / grading guide. Empty when the subject has no uploaded rubric file.
    IReadOnlyList<AiGradeFileRefPayload>? RubricFiles = null,
    // The approved compiled-rubric version whose contract populated the score-grid items below
    // (null when no approved contract exists — grader falls back to the raw-file path).
    string? CompiledRubricVersion = null,
    // Feedback language contract (doc 5.8). Defaults to Vietnamese.
    string Language = "vi",
    // Presigned page images of the student paper for multimodal grading (Phase 3).
    IReadOnlyList<string>? StudentPageImageUrls = null
);

public record AiGradeFileRefPayload(string Url, string ContentType);

public record AiGradeCheckItemPayload(
    string CheckId,
    string Description,
    decimal Points,
    bool Required,
    string? EvidenceHint
);

public record AiGradePartialCreditPayload(
    string Label,
    decimal Score,
    string Condition,
    IReadOnlyList<string> CheckIds
);

public record AiGradeScoreGridItemPayload(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore,
    // ── Compiled-contract enrichment (Phase 3a; all optional/empty when no approved contract) ──
    string? ParentQuestion = null,
    string? QuestionText = null,
    string? AnswerKey = null,
    IReadOnlyList<AiGradeCheckItemPayload>? CheckItems = null,
    IReadOnlyList<AiGradePartialCreditPayload>? PartialCredit = null,
    IReadOnlyList<string>? CommonMistakes = null,
    IReadOnlyList<string>? NotPenalize = null,
    bool RequiresVisual = false,
    IReadOnlyList<string>? VisualAssetUrls = null,
    string? Specificity = null,
    string? ExtractionConfidence = null
);
