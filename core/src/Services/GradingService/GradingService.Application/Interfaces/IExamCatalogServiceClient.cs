using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface IExamCatalogServiceClient
{
    Task<SubjectGradingGridClientDto?> GetGradingGridAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Minimal exam context (code, exam name, exam end date) for a subject.
    /// Returns null if the subject doesn't exist or ExamCatalogService is unreachable.
    /// </summary>
    Task<SubjectExamInfoClientDto?> GetExamInfoAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>Plain-text rubric summary for AI grading. Null if unreachable.</summary>
    Task<string?> GetRubricTextAsync(Guid subjectId, CancellationToken ct = default);
}
