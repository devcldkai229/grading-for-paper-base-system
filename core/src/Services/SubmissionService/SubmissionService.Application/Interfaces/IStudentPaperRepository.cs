using SubmissionService.Application.DTOs;

namespace SubmissionService.Application.Interfaces;

public interface IStudentPaperRepository
{
    /// <summary>
    /// Lists papers for a subject.
    /// <paramref name="uploadedByFilter"/> when set restricts to papers uploaded by that user (lecturer self-service).
    /// </summary>
    Task<(IReadOnlyList<StudentPaperDto> Items, int TotalCount)> GetPapersAsync(
        Guid subjectId, string? status, int page, int pageSize,
        Guid? uploadedByFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Keyword search across every subject's papers (unlike GetPapersAsync, not scoped to one
    /// subject) — matches StudentAlias (case-insensitive substring) or an exact AliasNumber when
    /// the keyword parses as an integer. Consumed by the global search box.
    /// <paramref name="uploadedByFilter"/> when set restricts to papers uploaded by that user (lecturer self-service).
    /// </summary>
    Task<(IReadOnlyList<StudentPaperDto> Items, int TotalCount)> SearchPapersAsync(
        string keyword, int page, int pageSize,
        Guid? uploadedByFilter = null,
        CancellationToken ct = default);

    Task<BatchPapersDto?> GetPapersByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Batch header (existence + ownership) only. Null when the batch does not exist.
    /// Paired with <see cref="StreamPapersByBatchAsync"/> so the gRPC path can emit a header before
    /// streaming papers without first materializing the whole batch in memory.</summary>
    Task<BatchSummaryDto?> GetBatchSummaryAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Streams a batch's papers ordered by alias number using a server-side cursor (no
    /// full ToListAsync). Yields nothing when the batch has no papers or does not exist — callers
    /// should check existence via <see cref="GetBatchSummaryAsync"/> first.</summary>
    IAsyncEnumerable<BatchPaperDto> StreamPapersByBatchAsync(Guid batchId, CancellationToken ct = default);

    Task<InternalPaperSummaryDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default);

    /// <summary>Bulk-resolves summaries (alias, batch, subject) for an arbitrary set of paper ids
    /// in one round trip. Consumed by GradingService to attach aliases to a lecturer's grading queue.</summary>
    Task<IReadOnlyList<InternalPaperSummaryDto>> GetPapersByIdsAsync(
        IReadOnlyCollection<Guid> paperIds, CancellationToken ct = default);

    Task<(Guid PaperId, string StudentAlias)> CreateSinglePaperWithFilesAsync(
        Guid batchId, Guid subjectId, Guid uploadedBy, int aliasNumber,
        IReadOnlyList<(string FileName, string ContentType, long SizeBytes, string S3Key)> files,
        CancellationToken ct = default);

    Task<StudentPaperDetailDto?> GetPaperDetailAsync(Guid paperId, CancellationToken ct = default);

    Task<(string S3Key, string FileName, string ContentType)?> GetPaperFileInfoAsync(
        Guid paperId, Guid fileId, CancellationToken ct = default);

    /// <summary>Deletion pre-check info (status + S3 keys) for a single paper. Null if the paper does not exist.</summary>
    Task<PaperDeletionInfoDto?> GetPaperForDeletionAsync(Guid paperId, CancellationToken ct = default);

    /// <summary>Deletion pre-check info (status + S3 keys) for every paper in a batch.</summary>
    Task<IReadOnlyList<PaperDeletionInfoDto>> GetPapersForDeletionAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Removes the paper's file records and the paper record itself. Caller must have already verified gradability and purged S3 objects.</summary>
    Task DeletePaperAsync(Guid paperId, CancellationToken ct = default);

    /// <summary>Removes all paper and file records belonging to a batch. Caller must have already verified gradability and purged S3 objects.</summary>
    Task DeletePapersByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Total paper count and highest alias number submitted for a subject (across every
    /// batch). Consumed internally by GradingService to validate marker-assignment alias ranges.</summary>
    Task<SubjectPaperStatsDto> GetSubjectPaperStatsAsync(Guid subjectId, CancellationToken ct = default);
}
