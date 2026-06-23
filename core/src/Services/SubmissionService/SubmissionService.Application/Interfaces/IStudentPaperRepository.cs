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
}
