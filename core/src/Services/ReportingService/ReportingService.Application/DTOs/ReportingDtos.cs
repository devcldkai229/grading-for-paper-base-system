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
    DateTime? EstimatedFinish
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
    IReadOnlyList<LecturerProgressClientDto> Lecturers
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
    string SemesterCode
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
    IReadOnlyList<LecturerProgressClientDto> Lecturers
);

public record GradingProgressDashboardResultDto(
    IReadOnlyList<GradingProgressSubjectDto> Subjects
);
