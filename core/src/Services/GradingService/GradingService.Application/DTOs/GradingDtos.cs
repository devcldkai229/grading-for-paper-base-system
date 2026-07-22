namespace GradingService.Application.DTOs;

public record AssignmentSummaryDto(
    Guid AssignmentId,
    Guid PaperId,
    int? AliasNumber,
    string Status
);

public record StartBatchResultDto(
    Guid FirstAssignmentId,
    IReadOnlyList<Guid> AssignmentIds,
    Guid SubjectId,
    Guid BatchId,
    IReadOnlyList<AssignmentSummaryDto> Assignments
);

public record QuestionMarkDto(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore,
    decimal? Score,
    string? QuestionComment,
    int OrderIndex,
    bool AiDrafted = false,
    string? AiReviewStatus = null
);

public record GradingSessionDto(
    Guid AssignmentId,
    Guid StudentPaperId,
    Guid SubjectId,
    Guid BatchId,
    decimal SubjectMaxScore,
    int RubricVersion,
    string Status,
    string AiStatus,
    int RowVersion,
    string? PaperComment,
    string? InternalComment,
    string? StudentAlias,
    int? AliasNumber,
    IReadOnlyList<QuestionMarkDto> Questions,
    bool IsFlagged = false,
    IReadOnlyList<AiSuggestionDto>? AiSuggestions = null,
    string? AiPaperComment = null
);

/// <summary>One AI grading suggestion for a leaf question, kept separate from the manual mark.
/// The lecturer copies these into the manual marks explicitly (per-question or all at once).</summary>
public record AiSuggestionDto(
    string QuestionNumber,
    decimal Score,
    string? QuestionComment,
    double Confidence,
    bool IsManualOnly
);

public record SaveMarksRequest(
    int RowVersion,
    IReadOnlyList<QuestionMarkInput> Questions,
    string? PaperComment,
    string? InternalComment
);

public record QuestionMarkInput(
    string QuestionNumber,
    decimal Score,
    string? QuestionComment
);

public record SaveMarksResultDto(int RowVersion);

public record SubmitResultDto(Guid? NextAssignmentId);

public record GradingQueueRowDto(
    Guid AssignmentId,
    string? StudentAlias,
    int? AliasNumber,
    Guid SubjectId,
    string Status,
    bool IsFlagged,
    decimal? TotalScore,
    DateTime? SubmittedAt,
    Guid? BatchId = null,
    string? ZipFileName = null
);

public record GradingQueueFolderDto(
    Guid BatchId,
    string? ZipFileName,
    Guid SubjectId,
    int TotalPapers,
    int NotStarted,
    int Drafting,
    int Submitted,
    DateTime AssignedAt
);

public record GradingQueuePageDto(
    IReadOnlyList<GradingQueueRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

public record SetFlagRequest(bool IsFlagged);

public record OverrideMarksRequest(
    IReadOnlyList<QuestionMarkInput> Questions,
    string? PaperComment,
    string? InternalComment,
    string Reason
);

public record OverrideMarksResultDto(int RowVersion, decimal TotalScore);

public record AuditLogEntryDto(
    Guid Id,
    Guid UserId,
    string Action,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime CreatedAt
);

public record BatchPapersClientDto(
    Guid BatchId,
    Guid SubjectId,
    Guid UploadedBy,
    IReadOnlyList<BatchPaperClientDto> Papers
);

public record BatchPaperClientDto(Guid PaperId, Guid SubjectId, int? AliasNumber);

public record SubjectPaperStatsClientDto(int TotalPapers, int? MaxAliasNumber);

public record InternalPaperSummaryClientDto(
    Guid Id,
    Guid BatchId,
    Guid SubjectId,
    string? StudentAlias,
    int? AliasNumber,
    Guid UploadedBy
);

public record SubjectGradingGridClientDto(
    Guid SubjectId,
    decimal MaxScore,
    int RubricVersion,
    IReadOnlyList<SubjectQuestionClientDto> Questions,
    string? Status = null
);

public record QuestionFeedbackDto(
    string QuestionNumber,
    string? Label,
    decimal Score,
    decimal MaxScore,
    string? QuestionComment
);

public record StudentFeedbackDto(
    Guid StudentPaperId,
    int? AliasNumber,
    string? StudentAlias,
    decimal TotalScore,
    string? PaperComment,
    IReadOnlyList<QuestionFeedbackDto> Questions
);

public record SubjectFeedbackExportDto(
    Guid SubjectId,
    IReadOnlyList<StudentFeedbackDto> Students
);

public record SubjectExamInfoClientDto(
    Guid SubjectId,
    string SubjectCode,
    Guid ExamId,
    string ExamName,
    DateOnly? ExamEndDate,
    DateOnly? GradingDeadline = null
);

public record MyProgressDto(
    int TotalAssignments,
    int SubmittedCount,
    int RemainingCount,
    Guid? NextAssignmentId,
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines
);

public record UpcomingDeadlineDto(
    Guid SubjectId,
    string SubjectCode,
    string ExamName,
    DateOnly Deadline,
    int TotalCount,
    int SubmittedCount,
    int RemainingCount,
    Guid? NextAssignmentId
);

public record SubjectQuestionClientDto(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore,
    int OrderIndex
);

/// <summary>One lecturer's grading progress within a single subject.</summary>
public record LecturerProgressDto(
    Guid TeacherId,
    int AssignedCount,
    int CompletedCount,
    int DraftingCount,
    int NotStartedCount,
    decimal? AvgScore,
    decimal? ThroughputPerHour,
    DateTime? LastActivityAt,
    DateTime? EstimatedFinish,
    decimal? AvgGradingMinutesPerPaper,
    int FlaggedCount = 0
);

/// <summary>Aggregate grading progress for one subject, broken down per lecturer.</summary>
public record SubjectProgressDto(
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
    IReadOnlyList<LecturerProgressDto> Lecturers,
    decimal? AvgGradingMinutesPerPaper,
    int FlaggedCount = 0
);

public record RecordHeartbeatRequest(int Seconds);

public record GradingProgressDashboardDto(
    IReadOnlyList<SubjectProgressDto> Subjects
);

/// <summary>Raw submitted scores for one subject, for the admin score distribution report.
/// Bucketing into a histogram happens in ReportingService (it knows the subject's MaxScore).</summary>
public record SubjectScoreDistributionDto(
    Guid SubjectId,
    int SubmittedCount,
    decimal? ScoreAvg,
    decimal? ScoreMin,
    decimal? ScoreMax,
    IReadOnlyList<decimal> Scores
);

public record ScoreDistributionDashboardDto(
    IReadOnlyList<SubjectScoreDistributionDto> Subjects
);

/// <summary>One audit entry, unscoped (not tied to a single caller-known entity). Used by the
/// admin global audit log viewer, unlike AuditLogEntryDto which is scoped to one assignment.</summary>
public record AuditLogRecordDto(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime CreatedAt
);

public record AuditLogPageDto(
    IReadOnlyList<AuditLogRecordDto> Items,
    int TotalCount
);

public record MarkerAssignmentDto(
    Guid Id,
    Guid SubjectId,
    Guid TeacherId,
    int AliasStart,
    int AliasEnd,
    Guid AssignedBy,
    DateTime AssignedAt,
    Guid? BatchId = null,
    string? ZipFileName = null
);

public record CreateFolderAssignmentRequest(
    Guid TeacherId,
    Guid BatchId,
    string? ZipFileName
);

/// <summary>Either (AliasStart, AliasEnd) or Quota must be provided; Quota auto-picks the next
/// contiguous, non-overlapping range for the subject (starting right after the highest AliasEnd
/// already assigned, or at 1 if none).</summary>
public record CreateMarkerAssignmentRequest(
    Guid TeacherId,
    int? AliasStart,
    int? AliasEnd,
    int? Quota
);

public record ReassignMarkerAssignmentRequest(
    Guid TeacherId,
    int AliasStart,
    int AliasEnd
);

public record MaterializeResultDto(
    int MaterializedCount,
    int SkippedInProgressCount,
    IReadOnlyList<string> Warnings
);

public record MarkerAssignmentResultDto(
    MarkerAssignmentDto Assignment,
    int MaterializedCount,
    int SkippedInProgressCount,
    IReadOnlyList<string> Warnings
);

// ---------------------------------------------------------------------------
// AI Grading — write-back (AIGradingService → GradingService)
// ---------------------------------------------------------------------------

/// <summary>Payload from AIGradingService to apply AI-generated score suggestions to an assignment.</summary>
public record ApplyAiSuggestionsRequest(
    Guid AssignmentId,
    IReadOnlyList<AiQuestionGrade> QuestionGrades,
    string ModelUsed,
    string PromptVersion,
    string? PaperComment = null
);

public record AiQuestionGrade(
    string QuestionNumber,
    decimal Score,
    string? QuestionComment,
    double Confidence,
    bool IsManualOnly
);

// ---------------------------------------------------------------------------
// AI Grading — outbound (GradingService → AIGradingService)
// ---------------------------------------------------------------------------

/// <summary>Request sent to AIGradingService to grade a paper.</summary>
public record AiGradeSuggestRequest(
    Guid AssignmentId,
    Guid SubjectId,
    string RubricVersion,
    IReadOnlyList<AiGradeFileRef> Files,
    IReadOnlyList<AiGradeScoreGridItem> ScoreGrid,
    string? RubricText
);

public record AiGradeFileRef(string Url, string ContentType);

/// <summary>Presigned URL to an original rubric/exam source file for AI grading context.
/// Kind is "rubric" or "exam_paper".</summary>
public record RubricSourceFileClientDto(string Url, string ContentType, string Kind);

/// <summary>Approved compiled grading contract for a subject (contract JSON + presigned asset URLs).</summary>
public record CompiledRubricClientDto(
    int RubricVersion,
    string Status,
    string ContractJson,
    bool CoverageOk,
    IReadOnlyList<CompiledRubricAssetClientDto> Assets);

public record CompiledRubricAssetClientDto(
    string AssetId, string Url, string ContentType, string? Question);

public record AiGradeScoreGridItem(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore
);
