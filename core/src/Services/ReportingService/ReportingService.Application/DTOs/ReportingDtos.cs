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
