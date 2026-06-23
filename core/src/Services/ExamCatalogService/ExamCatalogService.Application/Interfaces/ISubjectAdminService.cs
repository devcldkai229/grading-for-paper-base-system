using ExamCatalogService.Application.DTOs;

namespace ExamCatalogService.Application.Interfaces;

public interface ISubjectAdminService
{
    (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateQuestions(
        decimal subjectMaxScore,
        IReadOnlyList<QuestionInputDto> questions);
}
