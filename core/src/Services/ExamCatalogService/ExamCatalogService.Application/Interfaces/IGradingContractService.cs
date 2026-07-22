namespace ExamCatalogService.Application.Interfaces;

/// <summary>
/// Orchestrates compilation of a subject's barem into a reviewable grading contract: presign the
/// rubric file, call the AI ingestion pipeline, persist illustration crops to S3, store the contract
/// (status = Pending), and publish <c>RubricCompiledEvent</c>. The contract is only usable for AI
/// grading once an admin approves it.
/// </summary>
public interface IGradingContractService
{
    /// <summary>Compile + persist the contract for the subject's current rubric version.
    /// Returns false when there is no rubric file or the AI service is unreachable.</summary>
    Task<bool> IngestAsync(Guid subjectId, CancellationToken ct = default);
}
