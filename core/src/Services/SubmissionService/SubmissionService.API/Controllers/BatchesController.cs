using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application;
using SubmissionService.Application.Interfaces;
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

    public BatchesController(
        IBatchRepository batchRepository,
        IStudentPaperRepository paperRepository,
        IPublishEndpoint publishEndpoint,
        IS3Service s3Service)
    {
        _batchRepository = batchRepository;
        _paperRepository = paperRepository;
        _publishEndpoint = publishEndpoint;
        _s3Service = s3Service;
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
}
