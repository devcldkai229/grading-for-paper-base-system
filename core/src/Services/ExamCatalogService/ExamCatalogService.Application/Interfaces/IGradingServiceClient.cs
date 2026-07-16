namespace ExamCatalogService.Application.Interfaces;

public interface IGradingServiceClient
{
    /// <summary>
    /// Returns the subject ids a lecturer has any marker assignment for.
    /// Returns null (not empty) when GradingService is unreachable — callers must fail-closed
    /// (deny/empty-result) rather than fall back to showing everything.
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetAssignedSubjectIdsAsync(Guid lecturerId, CancellationToken ct = default);
}
