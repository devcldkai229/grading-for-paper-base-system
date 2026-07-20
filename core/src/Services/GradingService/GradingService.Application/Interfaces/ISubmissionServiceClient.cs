using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface ISubmissionServiceClient
{
    Task<BatchPapersClientDto?> GetBatchPapersAsync(Guid batchId, CancellationToken ct = default);

    Task<InternalPaperSummaryClientDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default);

    /// <summary>
    /// Aggregate paper stats (total count + highest submitted alias number) for a subject. Null
    /// when SubmissionService is unreachable — callers should treat that as "unknown, skip validation"
    /// rather than fail closed (same graceful-degrade convention as the other client methods here).
    /// </summary>
    Task<SubjectPaperStatsClientDto?> GetSubjectPaperStatsAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-resolves summaries (alias, batch, subject) for an arbitrary set of paper ids in one
    /// round trip. Null when SubmissionService is unreachable — callers should degrade gracefully
    /// (e.g. show the queue row without an alias) rather than fail the whole request.
    /// </summary>
    Task<IReadOnlyList<InternalPaperSummaryClientDto>?> GetPaperSummariesAsync(
        IReadOnlyCollection<Guid> paperIds, CancellationToken ct = default);
}
