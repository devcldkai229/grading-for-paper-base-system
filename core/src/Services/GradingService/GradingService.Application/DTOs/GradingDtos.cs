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
    int OrderIndex
);

public record GradingSessionDto(
    Guid AssignmentId,
    Guid StudentPaperId,
    Guid SubjectId,
    Guid BatchId,
    decimal SubjectMaxScore,
    int RubricVersion,
    string Status,
    int RowVersion,
    string? PaperComment,
    string? InternalComment,
    string? StudentAlias,
    int? AliasNumber,
    IReadOnlyList<QuestionMarkDto> Questions
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
    DateOnly? ExamEndDate
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
    DateOnly ExamEndDate,
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
    decimal? AvgGradingMinutesPerPaper
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
    decimal? AvgGradingMinutesPerPaper
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
    DateTime AssignedAt
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
