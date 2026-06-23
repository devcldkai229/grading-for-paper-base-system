using BuildingBlocks.AwsS3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using System.Security.Claims;

namespace SubmissionService.API.Controllers;

[Route("api/submissions")]
[ApiController]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly TimeSpan _presignedUrlTtl;

    public SubmissionsController(
        IStudentPaperRepository paperRepository,
        IS3Service s3Service,
        IOptions<AwsS3Settings> s3Settings)
    {
        _paperRepository = paperRepository;
        _s3Service = s3Service;
        _presignedUrlTtl = s3Settings.Value.PresignedUrlTtl;
    }

    /// <summary>
    /// List student papers for a subject.
    /// Admin: sees all. Lecturer: only papers they uploaded (self-service).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubmissions(
        [FromQuery] Guid subjectId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var role = User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value;

        Guid? uploadedByFilter = null;

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdString, out var lecturerId))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    StatusCode = 401,
                    Message = "Invalid user ID",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            uploadedByFilter = lecturerId;
        }

        var (items, totalCount) = await _paperRepository.GetPapersAsync(
            subjectId, status, page, pageSize, uploadedByFilter, ct);

        return Ok(new ApiResponse<PagedResult<StudentPaperDto>>
        {
            StatusCode = 200,
            Message = "Submissions retrieved successfully",
            Data = new PagedResult<StudentPaperDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            },
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get student paper detail with list of paper files (sorted by order_index).
    /// </summary>
    [HttpGet("{paperId:guid}")]
    public async Task<IActionResult> GetSubmissionDetail(
        Guid paperId,
        CancellationToken ct = default)
    {
        var detail = await _paperRepository.GetPaperDetailAsync(paperId, ct);
        if (detail is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Submission not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<StudentPaperDetailDto>
        {
            StatusCode = 200,
            Message = "Submission detail retrieved successfully",
            Data = detail,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Generate a short-lived pre-signed URL for a paper file.
    /// Does not expose s3_key.
    /// </summary>
    [HttpGet("{paperId:guid}/files/{fileId:guid}/url")]
    public async Task<IActionResult> GetFileUrl(
        Guid paperId,
        Guid fileId,
        CancellationToken ct = default)
    {
        var info = await _paperRepository.GetPaperFileInfoAsync(paperId, fileId, ct);
        if (info is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "File not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, fileName, contentType) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(s3Key, _presignedUrlTtl, ct);

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "File URL generated successfully",
            Data = new FileUrlResponse(url, contentType, fileName),
            ResponsedAt = DateTime.UtcNow
        });
    }
}
