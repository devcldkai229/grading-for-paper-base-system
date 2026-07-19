namespace ExamCatalogService.Application.DTOs;

public record SemesterDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    DateTime CreatedAt
);

public record ExamDto(
    Guid Id,
    Guid SemesterId,
    string Name,
    string ExamType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int SubjectCount,
    DateTime CreatedAt
);

public record SubjectSummaryDto(
    Guid Id,
    Guid ExamId,
    string SubjectCode,
    string? Title,
    decimal MaxScore,
    string Status,
    bool HasExamPaper,
    bool HasRubric,
    int QuestionCount,
    DateTime CreatedAt,
    decimal? PassScore = null
);

public record SubjectExamInfoDto(
    Guid SubjectId,
    string SubjectCode,
    Guid ExamId,
    string ExamName,
    DateOnly? ExamEndDate,
    DateOnly? GradingDeadline = null
);

public record SubjectSearchResultDto(
    Guid Id,
    Guid ExamId,
    string ExamName,
    Guid SemesterId,
    string SemesterCode,
    string SubjectCode,
    string? Title,
    decimal MaxScore,
    string Status,
    bool HasExamPaper,
    bool HasRubric,
    int QuestionCount,
    DateTime CreatedAt,
    decimal? PassScore = null,
    DateOnly? GradingDeadline = null
);

public record SubjectDetailDto(
    Guid Id,
    Guid ExamId,
    string SubjectCode,
    string? Title,
    decimal MaxScore,
    string Status,
    bool HasExamPaper,
    string? ExamPaperFileName,
    string? ExamPaperContentType,
    bool HasRubric,
    string? RubricFileName,
    string? RubricContentType,
    int RubricVersion,
    IReadOnlyList<QuestionDto> Questions,
    DateTime CreatedAt,
    decimal? PassScore = null,
    DateOnly? GradingDeadline = null
);

public record QuestionDto(
    Guid Id,
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore,
    int OrderIndex
);

public record CreateSemesterRequest(
    string Code,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive
);

public record UpdateSemesterRequest(
    string Code,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive
);

public record CreateExamRequest(
    Guid SemesterId,
    string Name,
    string ExamType,
    DateOnly? StartDate,
    DateOnly? EndDate
);

public record UpdateExamRequest(
    string Name,
    string ExamType,
    DateOnly? StartDate,
    DateOnly? EndDate
);

public record CreateSubjectRequest(
    Guid ExamId,
    string SubjectCode,
    string? Title,
    decimal MaxScore,
    string? Status = null,
    decimal? PassScore = null,
    DateOnly? GradingDeadline = null
);

public record UpdateSubjectRequest(
    string SubjectCode,
    string? Title,
    decimal MaxScore,
    string? Status = null,
    decimal? PassScore = null,
    DateOnly? GradingDeadline = null
);

public record QuestionInputDto(
    string? GroupLabel,
    string QuestionNumber,
    string? Label,
    decimal MaxScore,
    int OrderIndex
);

public record ReplaceQuestionsRequest(
    IReadOnlyList<QuestionInputDto> Questions
);

public record ReplaceQuestionsResponse(
    IReadOnlyList<QuestionDto> Questions,
    IReadOnlyList<string> Warnings
);

public record CreateScoreGridTemplateRequest(
    string Name,
    IReadOnlyList<QuestionInputDto> Questions
);

/// <summary>List-view row — omits the questions payload so listing templates never needs to
/// deserialize any JSON.</summary>
public record ScoreGridTemplateSummaryDto(
    Guid Id,
    string Name,
    Guid CreatedBy,
    int QuestionCount,
    DateTime CreatedAt
);

public record ScoreGridTemplateDetailDto(
    Guid Id,
    string Name,
    Guid CreatedBy,
    IReadOnlyList<QuestionInputDto> Questions,
    DateTime CreatedAt
);

public record ExtractedQuestionDto(
    string? GroupLabel,
    string QuestionNumber,
    string? Label,
    decimal MaxScore,
    double Confidence
);

public record ExtractGridResponse(
    IReadOnlyList<ExtractedQuestionDto> Questions,
    decimal TotalMax,
    IReadOnlyList<string> Warnings
);

public record RubricUploadResponse(
    int RubricVersion,
    string FileName,
    string ContentType
);

public record ExamPaperUploadResponse(
    string FileName,
    string ContentType
);

public record FileUrlResponse(
    string Url,
    string ContentType,
    string FileName,
    int? RubricVersion = null,
    bool HasOriginalDownload = false
);
