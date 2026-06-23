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

public record BatchPapersClientDto(
    Guid BatchId,
    Guid SubjectId,
    Guid UploadedBy,
    IReadOnlyList<BatchPaperClientDto> Papers
);

public record BatchPaperClientDto(Guid PaperId, Guid SubjectId, int? AliasNumber);

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
    IReadOnlyList<SubjectQuestionClientDto> Questions
);

public record SubjectQuestionClientDto(
    string QuestionNumber,
    string? GroupLabel,
    string? Label,
    decimal MaxScore,
    int OrderIndex
);
