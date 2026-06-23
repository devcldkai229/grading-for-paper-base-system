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
}
