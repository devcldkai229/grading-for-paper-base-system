using SubmissionService.Application.DTOs;

namespace SubmissionService.Application.Interfaces;

public interface IBatchService
{
    Task<ServiceResult<Guid>> UploadBatchAsync(
        Guid subjectId, Guid userId, UploadFileInput file, CancellationToken ct = default);

    Task<ServiceResult<CreateFileBatchResultDto>> UploadFilesAsync(
        Guid subjectId, Guid userId, IReadOnlyList<UploadFileInput> files, CancellationToken ct = default);

    Task<BatchStatusDto?> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default);

    Task<ServiceResult<Guid>> RetryBatchAsync(
        Guid batchId, Guid userId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<object?>> DeleteBatchAsync(
        Guid batchId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
