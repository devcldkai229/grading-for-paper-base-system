using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Enums;

namespace SubmissionService.Application.Services;

public class PaperQueryService : IPaperQueryService
{
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly ISubmissionAuditLogRepository _auditLogRepository;
    private readonly PresignedUrlOptions _presignedUrlOptions;

    public PaperQueryService(
        IStudentPaperRepository paperRepository,
        IS3Service s3Service,
        ISubmissionAuditLogRepository auditLogRepository,
        PresignedUrlOptions presignedUrlOptions)
    {
        _paperRepository = paperRepository;
        _s3Service = s3Service;
        _auditLogRepository = auditLogRepository;
        _presignedUrlOptions = presignedUrlOptions;
    }

    public async Task<PagedResult<StudentPaperDto>> GetPapersAsync(
        Guid subjectId, string? status, int page, int pageSize, Guid? uploadedByFilter, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _paperRepository.GetPapersAsync(
            subjectId, status, page, pageSize, uploadedByFilter, ct);

        return new PagedResult<StudentPaperDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<StudentPaperDetailDto?> GetPaperDetailAsync(Guid paperId, CancellationToken ct = default) =>
        _paperRepository.GetPaperDetailAsync(paperId, ct);

    public async Task<ServiceResult<FileUrlResponse>> GetFileUrlAsync(
        Guid paperId, Guid fileId, CancellationToken ct = default)
    {
        var info = await _paperRepository.GetPaperFileInfoAsync(paperId, fileId, ct);
        if (info is null)
        {
            return ServiceResult<FileUrlResponse>.NotFound();
        }

        var (s3Key, fileName, contentType) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(s3Key, _presignedUrlOptions.Ttl, ct);

        return ServiceResult<FileUrlResponse>.Ok(new FileUrlResponse(url, contentType, fileName));
    }

    public async Task<ServiceResult<object?>> DeletePaperAsync(
        Guid paperId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var paper = await _paperRepository.GetPaperForDeletionAsync(paperId, ct);
        if (paper is null)
        {
            return ServiceResult<object?>.NotFound();
        }

        if (!isAdmin && userId != paper.UploadedBy)
        {
            return ServiceResult<object?>.Forbidden();
        }

        if (paper.Status != PaperStatus.ReadyToAssign.ToString())
        {
            return ServiceResult<object?>.Conflict("This paper has already started grading; it can no longer be deleted.");
        }

        foreach (var file in paper.Files)
        {
            await _s3Service.DeleteAsync(file.S3Key, ct);
        }

        await _paperRepository.DeletePaperAsync(paperId, ct);

        await _auditLogRepository.LogAsync(
            "DeletePaper", "Paper", paperId, paper.SubjectId, userId,
            $"Deleted paper from batch {paper.BatchId}", ct);

        return ServiceResult<object?>.Ok(null);
    }
}
