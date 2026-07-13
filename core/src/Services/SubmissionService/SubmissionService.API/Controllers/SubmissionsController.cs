using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using System.Security.Claims;

namespace SubmissionService.API.Controllers;

[Route("api/submissions")]
[ApiController]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly IPaperQueryService _paperQueryService;

    public SubmissionsController(IPaperQueryService paperQueryService)
    {
        _paperQueryService = paperQueryService;
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
        // TokenService issues the role claim with literal Type "Role" (not the ClaimTypes.Role URI,
        // and not lowercase "role") — .NET's default inbound claim map only remaps "role" (lowercase),
        // so neither ClaimTypes.Role nor "role" ever matches a real token. Must match the exact case.
        var role = User.FindFirst("Role")?.Value;
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        Guid? uploadedByFilter = null;

        if (!isAdmin)
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

        var result = await _paperQueryService.GetPapersAsync(subjectId, status, page, pageSize, uploadedByFilter, ct);

        return Ok(new ApiResponse<PagedResult<StudentPaperDto>>
        {
            StatusCode = 200,
            Message = "Submissions retrieved successfully",
            Data = result,
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
        var detail = await _paperQueryService.GetPaperDetailAsync(paperId, ct);
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
        var result = await _paperQueryService.GetFileUrlAsync(paperId, fileId, ct);
        if (result.Status == OperationStatus.NotFound)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "File not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "File URL generated successfully",
            Data = result.Data!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Delete a single student paper and its files — only while it has not started grading yet.
    /// Lecturers may only delete papers they uploaded; Admins may delete any paper.
    /// Purges the paper's S3 objects before removing the DB records, and records an audit log entry.
    /// </summary>
    [HttpDelete("{paperId:guid}")]
    public async Task<IActionResult> DeletePaper(Guid paperId, CancellationToken ct = default)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new ApiResponse<object>
            {
                StatusCode = 401,
                Message = "Invalid user ID",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        // TokenService issues the role claim with literal Type "Role" (see GetSubmissions above).
        var role = User.FindFirst("Role")?.Value;
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        var result = await _paperQueryService.DeletePaperAsync(paperId, userId, isAdmin, ct);

        return result.Status switch
        {
            OperationStatus.NotFound => NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Submission not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            }),
            OperationStatus.Forbidden => Forbid(),
            OperationStatus.Conflict => Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = result.Error!,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            }),
            _ => Ok(new ApiResponse<object>
            {
                StatusCode = 200,
                Message = "Paper deleted successfully",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            })
        };
    }
}
