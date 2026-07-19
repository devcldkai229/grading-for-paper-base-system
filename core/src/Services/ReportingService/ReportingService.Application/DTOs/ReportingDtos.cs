namespace ReportingService.Application.DTOs;

public record QuestionFeedbackClientDto(
    string QuestionNumber,
    string? Label,
    decimal Score,
    decimal MaxScore,
    string? QuestionComment
);

public record StudentFeedbackClientDto(
    Guid StudentPaperId,
    int? AliasNumber,
    string? StudentAlias,
    decimal TotalScore,
    string? PaperComment,
    IReadOnlyList<QuestionFeedbackClientDto> Questions
);

public record SubjectFeedbackClientDto(
    Guid SubjectId,
    IReadOnlyList<StudentFeedbackClientDto> Students
);

/// <summary>One lecturer's grading progress within a single subject (mirrors GradingService's DTO).</summary>
public record LecturerProgressClientDto(
    Guid TeacherId,
    int AssignedCount,
    int CompletedCount,
    int DraftingCount,
    int NotStartedCount,
    decimal? AvgScore,
    decimal? ThroughputPerHour,
    DateTime? LastActivityAt,
    DateTime? EstimatedFinish,
    decimal? AvgGradingMinutesPerPaper
);

public record SubjectProgressClientDto(
    Guid SubjectId,
    int TotalPapers,
    int CompletedPapers,
    int DraftingPapers,
    int NotStartedPapers,
    decimal CompletionPercent,
    decimal? ScoreAvg,
    decimal? ScoreMin,
    decimal? ScoreMax,
    decimal? ThroughputPerHour,
    DateTime? EstimatedFinish,
    IReadOnlyList<LecturerProgressClientDto> Lecturers,
    decimal? AvgGradingMinutesPerPaper
);

public record GradingProgressDashboardClientDto(
    IReadOnlyList<SubjectProgressClientDto> Subjects
);

/// <summary>Subject metadata used to resolve a semester/exam filter into a concrete subject list.</summary>
public record SubjectFilterResultClientDto(
    Guid Id,
    string SubjectCode,
    string? Title,
    Guid ExamId,
    string ExamName,
    Guid SemesterId,
    string SemesterCode,
    decimal MaxScore,
    decimal? PassScore = null,
    DateOnly? GradingDeadline = null
);

/// <summary>One subject's grading progress dashboard row — subject metadata merged with progress numbers.</summary>
public record GradingProgressSubjectDto(
    Guid SubjectId,
    string SubjectCode,
    string? Title,
    string ExamName,
    string SemesterCode,
    int TotalPapers,
    int CompletedPapers,
    int DraftingPapers,
    int NotStartedPapers,
    decimal CompletionPercent,
    decimal? ScoreAvg,
    decimal? ScoreMin,
    decimal? ScoreMax,
    decimal? ThroughputPerHour,
    DateTime? EstimatedFinish,
    IReadOnlyList<LecturerProgressClientDto> Lecturers,
    decimal? AvgGradingMinutesPerPaper,
    DateOnly? GradingDeadline = null
);

public record GradingProgressDashboardResultDto(
    IReadOnlyList<GradingProgressSubjectDto> Subjects
);

/// <summary>Raw submitted scores + min/avg/max for one subject (mirrors GradingService's DTO).</summary>
public record SubjectScoreDistributionClientDto(
    Guid SubjectId,
    int SubmittedCount,
    decimal? ScoreAvg,
    decimal? ScoreMin,
    decimal? ScoreMax,
    IReadOnlyList<decimal> Scores
);

public record ScoreDistributionDashboardClientDto(
    IReadOnlyList<SubjectScoreDistributionClientDto> Subjects
);

/// <summary>One histogram bar: papers whose score falls in [RangeStart, RangeEnd).</summary>
public record ScoreHistogramBucketDto(
    decimal RangeStart,
    decimal RangeEnd,
    int Count
);

/// <summary>One subject's score distribution report row — subject metadata, stats and histogram.</summary>
public record SubjectScoreDistributionResultDto(
    Guid SubjectId,
    string SubjectCode,
    string? Title,
    string ExamName,
    string SemesterCode,
    decimal MaxScore,
    int SubmittedCount,
    decimal? ScoreAvg,
    decimal? ScoreMin,
    decimal? ScoreMax,
    IReadOnlyList<ScoreHistogramBucketDto> Histogram
);

public record ScoreDistributionResultDto(
    IReadOnlyList<SubjectScoreDistributionResultDto> Subjects
);

/// <summary>One subject's pass/fail counts against its configured PassScore threshold.
/// PassRatePercent/PassCount/FailCount are null when the subject has no PassScore configured yet.</summary>
public record SubjectPassFailResultDto(
    Guid SubjectId,
    string SubjectCode,
    string? Title,
    string ExamName,
    string SemesterCode,
    decimal MaxScore,
    decimal? PassScore,
    int SubmittedCount,
    int? PassCount,
    int? FailCount,
    decimal? PassRatePercent
);

public record PassFailReportResultDto(
    IReadOnlyList<SubjectPassFailResultDto> Subjects
);

/// <summary>One audit entry normalised across services (Iam/Grading/Submission each have their
/// own shape and field names — this is the common shape the global audit log viewer renders).</summary>
public record AuditLogEntryDto(
    string Source,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? Details,
    DateTime PerformedAt
);

public record GlobalAuditLogResultDto(
    IReadOnlyList<AuditLogEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<string> UnavailableSources
);

// --- Per-source client-side shapes (mirror each service's own audit log DTOs exactly, for
// JSON deserialization). Each client maps these into the common AuditLogEntryDto above. ---

public record IamAuditLogEntryClientDto(
    Guid Id, Guid? UserId, string Action, string EntityType, Guid EntityId,
    string? OldValue, string? NewValue, DateTime CreatedAt);

public record IamAuditLogPageClientDto(
    IReadOnlyList<IamAuditLogEntryClientDto> Items, int Page, int PageSize, int TotalCount);

public record GradingAuditLogEntryClientDto(
    Guid Id, Guid UserId, string Action, string EntityType, Guid? EntityId,
    string? OldValue, string? NewValue, string? Reason, DateTime CreatedAt);

public record GradingAuditLogPageClientDto(
    IReadOnlyList<GradingAuditLogEntryClientDto> Items, int TotalCount);

public record SubmissionAuditLogEntryClientDto(
    Guid Id, string Action, string EntityType, Guid EntityId, Guid SubjectId,
    Guid PerformedBy, string? Details, DateTime PerformedAt);

public record SubmissionAuditLogPageClientDto(
    IReadOnlyList<SubmissionAuditLogEntryClientDto> Items, int TotalCount);
