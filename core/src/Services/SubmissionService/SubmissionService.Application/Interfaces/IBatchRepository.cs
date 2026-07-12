using SubmissionService.Application.DTOs;
using SubmissionService.Domain.Enums;

namespace SubmissionService.Application.Interfaces;

public interface IBatchRepository
{
    Task<BatchDto> CreateBatchAsync(Guid subjectId, string zipS3Key, string? zipFileName,
        Guid uploadedBy, CancellationToken ct = default);

    Task<BatchStatusDto?> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default);

    Task UpdateBatchStatusAsync(Guid batchId, BatchStatus status, int? totalPapers = null,
        string? errorMessage = null, CancellationToken ct = default);

    Task<BatchDto?> GetBatchAsync(Guid batchId, CancellationToken ct = default);

    Task<BatchDto> CreateFileBatchAsync(Guid subjectId, Guid uploadedBy, CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions a batch from Failed back to Uploaded (clearing the error) so it can be
    /// re-queued for processing. Returns false if the batch is not currently Failed (e.g. already
    /// retried by a concurrent request, or still processing) — callers should treat false as "no-op".
    /// </summary>
    Task<bool> TryMarkFailedForRetryAsync(Guid batchId, CancellationToken ct = default);
}
