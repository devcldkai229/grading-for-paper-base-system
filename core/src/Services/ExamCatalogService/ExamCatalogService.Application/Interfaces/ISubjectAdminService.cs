using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Application.Interfaces;

public interface ISubjectAdminService
{
    (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateQuestions(
        decimal subjectMaxScore,
        IReadOnlyList<QuestionInputDto> questions);

    /// <summary>
    /// Enforces the subject status lifecycle (Draft -> Open -> Grading -> Closed): only a single
    /// forward step is allowed at a time; setting the same status is a no-op (always allowed);
    /// skips and backward moves are rejected. Returns null when valid, otherwise an error message.
    /// </summary>
    string? ValidateStatusTransition(SubjectStatus current, SubjectStatus requested);
}
