using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application;
using SubmissionService.Application.Interfaces;
using System.Security.Claims;

namespace SubmissionService.API.Controllers;

[Route("api/batches")]
[ApiController]
[Authorize]
public class BatchesController : ControllerBase
{
    // Keep aligned with ZipLimits.MaxTotalBytes (default 500 MB).
    private const long MaxUploadBytes = 524_288_000;

    private readonly IBatchService _batchService;

    public BatchesController(IBatchService batchService)
    {
        _batchService = batchService;
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
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims"));

        var stream = file?.OpenReadStream() ?? Stream.Null;
        try
        {
            var input = new UploadFileInput(file?.FileName ?? string.Empty, file?.Length ?? 0, stream);
            var result = await _batchService.UploadBatchAsync(subjectId, userId, input, ct);

            return result.Status switch
            {
                OperationStatus.Invalid => BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = result.Error!,
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                }),
                _ => Accepted(new ApiResponse<object>
                {
                    StatusCode = 202,
                    Message = "Batch upload accepted. Processing will begin shortly.",
                    Data = new { batchId = result.Data },
                    ResponsedAt = DateTime.UtcNow
                })
            };
        }
        finally
        {
            await stream.DisposeAsync();
        }
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

        var streams = new List<Stream>();
        try
        {
            var inputs = new List<UploadFileInput>();
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var stream = file.OpenReadStream();
                streams.Add(stream);
                inputs.Add(new UploadFileInput(file.FileName, file.Length, stream));
            }

            var result = await _batchService.UploadFilesAsync(subjectId, userId, inputs, ct);

            return result.Status switch
            {
                OperationStatus.Invalid => BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = result.Error!,
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                }),
                _ => Ok(new ApiResponse<Application.DTOs.CreateFileBatchResultDto>
                {
                    StatusCode = 200,
                    Message = "Files uploaded successfully",
                    Data = result.Data!,
                    ResponsedAt = DateTime.UtcNow
                })
            };
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Get batch status (for polling).
    /// </summary>
    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetBatchStatus(Guid batchId, CancellationToken ct = default)
    {
        var status = await _batchService.GetBatchStatusAsync(batchId, ct);
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
        // TokenService issues the role claim with literal Type "Role" — see SubmissionsController for details.
        var role = User.FindFirst("Role")?.Value;
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(userIdString, out var userId); // defaults to Guid.Empty on failure, same as the original ownership check

        var result = await _batchService.RetryBatchAsync(batchId, userId, isAdmin, ct);

        return result.Status switch
        {
            OperationStatus.NotFound => NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found",
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
            _ => Accepted(new ApiResponse<object>
            {
                StatusCode = 202,
                Message = "Batch retry accepted. Processing will begin shortly.",
                Data = new { batchId = result.Data },
                ResponsedAt = DateTime.UtcNow
            })
        };
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
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        var result = await _batchService.DeleteBatchAsync(batchId, userId, isAdmin, ct);

        return result.Status switch
        {
            OperationStatus.NotFound => NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found",
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
                Message = "Batch deleted successfully",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            })
        };
    }
}
