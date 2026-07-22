namespace Contracts.Messages;

/// <summary>
/// Published by ExamCatalogService when a compiled grading contract is (re)produced for a subject /
/// rubric version. Consumers (e.g. AIGradingService) use it to invalidate any cached suggestions for
/// the affected subject+version. The suggestion cache key already includes rubric_version, so a
/// version bump is naturally isolated; this event covers same-version recompiles/approvals.
/// </summary>
public record RubricCompiledEvent(
    Guid MessageId,
    Guid SubjectId,
    int RubricVersion,
    bool CoverageOk,
    Guid? CorrelationId,
    DateTime OccurredAt
);

/// <summary>
/// Published by ExamCatalogService when a subject's rubric file changes (new upload → new
/// RubricVersion). Signals that a fresh contract ingestion is required and prior AI grading for the
/// old version is stale.
/// </summary>
public record RubricVersionChangedEvent(
    Guid MessageId,
    Guid SubjectId,
    int RubricVersion,
    Guid? CorrelationId,
    DateTime OccurredAt
);
