using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IExamCatalogServiceClient
{
    /// <summary>
    /// Resolves a semester/exam filter into the matching subjects (metadata only — no progress
    /// numbers). Both filters null means "every subject". Returns null if ExamCatalogService is
    /// unreachable — callers must fail closed.
    /// </summary>
    Task<IReadOnlyList<SubjectFilterResultClientDto>?> SearchSubjectsAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default);
}
