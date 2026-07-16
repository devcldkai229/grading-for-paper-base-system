using SubmissionService.Application.DTOs;

namespace SubmissionService.Application.Interfaces;

public interface IPaperQueryService
{
    Task<PagedResult<StudentPaperDto>> GetPapersAsync(
        Guid subjectId, string? status, int page, int pageSize, Guid? uploadedByFilter, CancellationToken ct = default);

    /// <summary>
    /// Keyword search across every subject's papers (student alias / alias number), for the
    /// global search box. Blank keyword short-circuits to an empty page (no full scan).
    /// </summary>
    Task<PagedResult<StudentPaperDto>> SearchPapersAsync(
        string? keyword, int page, int pageSize, Guid? uploadedByFilter, CancellationToken ct = default);

    Task<StudentPaperDetailDto?> GetPaperDetailAsync(Guid paperId, CancellationToken ct = default);

    Task<ServiceResult<FileUrlResponse>> GetFileUrlAsync(
        Guid paperId, Guid fileId, CancellationToken ct = default);

    Task<ServiceResult<object?>> DeletePaperAsync(
        Guid paperId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
