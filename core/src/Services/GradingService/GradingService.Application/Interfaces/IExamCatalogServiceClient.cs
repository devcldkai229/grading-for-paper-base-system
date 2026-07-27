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

    /// <summary>Presigned URLs to the original barem file(s) (+ optional exam paper) so the AI reads
    /// the real rubric content. Empty list if none; null if ExamCatalog is unreachable.</summary>
    Task<IReadOnlyList<RubricSourceFileClientDto>?> GetRubricSourceFilesAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>The APPROVED compiled grading contract for a subject (optionally a specific version).
    /// Null when no approved contract exists or ExamCatalog is unreachable.</summary>
    Task<CompiledRubricClientDto?> GetCompiledRubricAsync(
        Guid subjectId, int? rubricVersion, CancellationToken ct = default);
}
