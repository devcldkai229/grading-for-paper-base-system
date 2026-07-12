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

    Task<BatchPapersDto?> GetPapersByBatchAsync(Guid batchId, CancellationToken ct = default);

    Task<InternalPaperSummaryDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default);

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
}
