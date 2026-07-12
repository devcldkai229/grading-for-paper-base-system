using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Enums;
using SubmissionService.Domain.Messages;
using System.Security.Claims;

namespace SubmissionService.API.Controllers;

[Route("api/batches")]
[ApiController]
[Authorize]
public class BatchesController : ControllerBase
{
    // Keep aligned with ZipLimits.MaxTotalBytes (default 500 MB).
    private const long MaxUploadBytes = 524_288_000;

    private readonly IBatchRepository _batchRepository;
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISubmissionAuditLogRepository _auditLogRepository;

    public BatchesController(
        IBatchRepository batchRepository,
        IStudentPaperRepository paperRepository,
        IPublishEndpoint publishEndpoint,
        IS3Service s3Service,
        ISubmissionAuditLogRepository auditLogRepository)
    {
        _batchRepository = batchRepository;
        _paperRepository = paperRepository;
        _publishEndpoint = publishEndpoint;
        _s3Service = s3Service;
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>
    /// Upload a ZIP file for a subject. Lecturers and Admins may upload.
    /// Returns 202 with batchId. Worker processes async.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadBatch(
        [FromForm] Guid subjectId,
        IFormFile file,
        CancellationToken ct = default)
    {
        // Validate file
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "File is required and must not be empty",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Only .zip files are accepted",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims"));

        var batchId = Guid.NewGuid();
        var s3Key = $"submissions/{subjectId}/{batchId}.zip";

        // Upload ZIP to S3
        using var stream = file.OpenReadStream();
        await _s3Service.UploadAsync(s3Key, stream, "application/zip", ct);

        // Create batch in MongoDB
        var batch = await _batchRepository.CreateBatchAsync(
            subjectId, s3Key, file.FileName, userId, ct);

        // Publish ParseBatchJob. MessageId = batch.Id => stable, so redelivery is deduped.
        await _publishEndpoint.Publish(new ParseBatchJob(
            batch.Id, subjectId, s3Key, userId, batch.Id), ct);

        return Accepted(new ApiResponse<object>
        {
            StatusCode = 202,
            Message = "Batch upload accepted. Processing will begin shortly.",
            Data = new { batchId = batch.Id },
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Upload multiple loose files as a single student paper (sync — no ZIP parse job).
    /// </summary>
    [HttpPost("files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadFiles(
        [FromForm] Guid subjectId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "At least one file is required",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims"));

        var validatedFiles = new List<(string FileName, string ContentType, long SizeBytes, IFormFile File)>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            if (!PaperContentTypes.IsAllowed(file.FileName, out var contentType))
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = $"File type not allowed: {file.FileName}",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            validatedFiles.Add((file.FileName, contentType, file.Length, file));
        }

        if (validatedFiles.Count == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "No valid files to upload",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var batch = await _batchRepository.CreateFileBatchAsync(subjectId, userId, ct);
        const int aliasNumber = 1;
        var studentAlias = $"Student_{aliasNumber:D4}";

        var uploaded = new List<(string FileName, string ContentType, long SizeBytes, string S3Key)>();
        foreach (var (fileName, contentType, sizeBytes, file) in validatedFiles)
        {
            var s3Key = $"submissions/{subjectId}/{studentAlias}/{fileName}";
            await using var stream = file.OpenReadStream();
            await _s3Service.UploadAsync(s3Key, stream, contentType, ct);
            uploaded.Add((fileName, contentType, sizeBytes, s3Key));
        }

        var (paperId, _) = await _paperRepository.CreateSinglePaperWithFilesAsync(
            batch.Id, subjectId, userId, aliasNumber, uploaded, ct);

        return Ok(new ApiResponse<Application.DTOs.CreateFileBatchResultDto>
        {
            StatusCode = 200,
            Message = "Files uploaded successfully",
            Data = new Application.DTOs.CreateFileBatchResultDto(batch.Id, paperId),
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get batch status (for polling).
    /// </summary>
    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetBatchStatus(Guid batchId, CancellationToken ct = default)
    {
        var status = await _batchRepository.GetBatchStatusAsync(batchId, ct);
        if (status is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<Application.DTOs.BatchStatusDto>
        {
            StatusCode = 200,
            Message = "Batch status retrieved",
            Data = status,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Re-run processing for a failed batch. Lecturers may only retry their own batches;
    /// Admins may retry any batch. Idempotent: only a batch currently in Failed status is
    /// re-queued, so repeated calls (or concurrent calls) publish at most one processing job.
    /// </summary>
    [HttpPost("{batchId:guid}/retry")]
    public async Task<IActionResult> RetryBatch(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, ct);
        if (batch is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        // TokenService issues the role claim with literal Type "Role" — see SubmissionsController for details.
        var role = User.FindFirst("Role")?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdString, out var requesterId) || requesterId != batch.UploadedBy)
            {
                return Forbid();
            }
        }

        var marked = await _batchRepository.TryMarkFailedForRetryAsync(batchId, ct);
        if (!marked)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = $"Batch cannot be retried from its current status ({batch.Status}). Only Failed batches can be retried.",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        // Reuse batch.Id as MessageId, matching the original upload publish (BatchesController.UploadBatch):
        // the consumer's Redis dedupe claim for this MessageId was released when the prior run failed,
        // so this republish is safe to process and reuses the same idempotent upsert logic (no duplicate papers).
        await _publishEndpoint.Publish(new ParseBatchJob(
            batch.Id, batch.SubjectId, batch.ZipS3Key, batch.UploadedBy, batch.Id), ct);

        return Accepted(new ApiResponse<object>
        {
            StatusCode = 202,
            Message = "Batch retry accepted. Processing will begin shortly.",
            Data = new { batchId = batch.Id },
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Delete a batch, its papers, and their files — only while none of its papers has started
    /// grading yet. Lecturers may only delete their own batches; Admins may delete any batch.
    /// Purges S3 objects (zip + every paper file) before removing the DB records, and records
    /// an audit log entry for the deletion.
    /// </summary>
    [HttpDelete("{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, ct);
        if (batch is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

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

        // TokenService issues the role claim with literal Type "Role" — see SubmissionsController for details.
        var role = User.FindFirst("Role")?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && userId != batch.UploadedBy)
        {
            return Forbid();
        }

        if (batch.Status == BatchStatus.Extracting.ToString())
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Batch is still being processed; try again once it finishes.",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var papers = await _paperRepository.GetPapersForDeletionAsync(batchId, ct);
        if (papers.Any(p => p.Status != PaperStatus.ReadyToAssign.ToString()))
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Batch has papers that already started grading; it can no longer be deleted.",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        foreach (var file in papers.SelectMany(p => p.Files))
        {
            await _s3Service.DeleteAsync(file.S3Key, ct);
        }
        if (!string.IsNullOrEmpty(batch.ZipS3Key))
        {
            await _s3Service.DeleteAsync(batch.ZipS3Key, ct);
        }

        await _paperRepository.DeletePapersByBatchAsync(batchId, ct);
        await _batchRepository.DeleteBatchRecordAsync(batchId, ct);

        await _auditLogRepository.LogAsync(
            "DeleteBatch", "Batch", batchId, batch.SubjectId, userId,
            $"Deleted batch '{batch.ZipFileName}' ({papers.Count} paper(s))", ct);

        return Ok(new ApiResponse<object>
        {
            StatusCode = 200,
            Message = "Batch deleted successfully",
            Data = null!,
            ResponsedAt = DateTime.UtcNow
        });
    }
}
