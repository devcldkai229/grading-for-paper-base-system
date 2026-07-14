using BuildingBlocks.AwsS3;
using ExamCatalogService.API.Authorization;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ExamCatalogService.API.Controllers;

[Route("api/subjects")]
[ApiController]
[Authorize]
public class SubjectsController : ControllerBase
{
    private const long MaxUploadBytes = 52_428_800; // 50 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/plain"
    };

    private readonly IExamCatalogRepository _repository;
    private readonly IS3Service _s3Service;
    private readonly IAiGradingClient _aiGradingClient;
    private readonly ISubjectQueryService _subjectQueryService;
    private readonly ISubjectAdminService _subjectAdminService;
    private readonly SubjectFilePreviewService _filePreviewService;
    private readonly TimeSpan _presignedUrlTtl;

    public SubjectsController(
        IExamCatalogRepository repository,
        IS3Service s3Service,
        IAiGradingClient aiGradingClient,
        ISubjectQueryService subjectQueryService,
        ISubjectAdminService subjectAdminService,
        SubjectFilePreviewService filePreviewService,
        IOptions<AwsS3Settings> s3Settings)
    {
        _repository = repository;
        _s3Service = s3Service;
        _aiGradingClient = aiGradingClient;
        _subjectQueryService = subjectQueryService;
        _subjectAdminService = subjectAdminService;
        _filePreviewService = filePreviewService;
        _presignedUrlTtl = s3Settings.Value.PresignedUrlTtl;
    }

    /// <summary>
    /// Search subjects by code, semester, exam and/or status (paginated).
    /// Admin sees all matching subjects; Lecturer only sees subjects they have a marker
    /// assignment for (fetched from GradingService — fails closed to an empty result if unreachable).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchSubjects(
        [FromQuery] string? code,
        [FromQuery] Guid? semesterId,
        [FromQuery] Guid? examId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        Guid? lecturerId = null;
        if (!IsAdmin())
        {
            var lecturerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(lecturerIdString, out var parsedLecturerId))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    StatusCode = 401,
                    Message = "Invalid user ID",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }
            lecturerId = parsedLecturerId;
        }

        var searchResult = await _subjectQueryService.SearchSubjectsAsync(
            code, semesterId, examId, status, lecturerId, page, pageSize, ct);

        if (searchResult.Error is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = searchResult.Error,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var result = new PagedResult<SubjectSearchResultDto>
        {
            Items = searchResult.Items,
            Page = searchResult.Page,
            PageSize = searchResult.PageSize,
            TotalCount = searchResult.TotalCount
        };

        return Ok(new ApiResponse<PagedResult<SubjectSearchResultDto>>
        {
            StatusCode = 200,
            Message = "Subjects retrieved successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get subject detail including score grid (questions) and file metadata.
    /// </summary>
    [HttpGet("{subjectId:guid}")]
    public async Task<IActionResult> GetSubjectDetail(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<SubjectDetailDto>
        {
            StatusCode = 200,
            Message = "Subject detail retrieved successfully",
            Data = subject,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Generate a short-lived pre-signed URL for inline exam paper preview.
    /// </summary>
    [HttpGet("{subjectId:guid}/exam-paper/url")]
    public async Task<IActionResult> GetExamPaperUrl(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var info = await _repository.GetExamPaperViewInfoAsync(subjectId, ct);
        if (info is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Exam paper not found for this subject",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, fileName, contentType, originalContentType) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(
            s3Key, _presignedUrlTtl, fileName, inline: true, ct);

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "Exam paper URL generated successfully",
            Data = new FileUrlResponse(
                url,
                contentType,
                fileName,
                HasOriginalDownload: SubjectFilePreviewService.IsDocx(originalContentType)),
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Download original exam paper file (DOCX only).
    /// </summary>
    [HttpGet("{subjectId:guid}/exam-paper/original")]
    public async Task<IActionResult> GetExamPaperOriginalUrl(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var info = await _repository.GetExamPaperOriginalInfoAsync(subjectId, ct);
        if (info is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Original DOCX exam paper not available",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, fileName, contentType) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(
            s3Key, _presignedUrlTtl, fileName, inline: false, ct);

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "Original exam paper URL generated successfully",
            Data = new FileUrlResponse(url, contentType, fileName),
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Generate a short-lived pre-signed URL for inline rubric preview.
    /// </summary>
    [HttpGet("{subjectId:guid}/rubric/url")]
    public async Task<IActionResult> GetRubricUrl(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var info = await _repository.GetRubricViewInfoAsync(subjectId, ct);
        if (info is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Rubric not found for this subject",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, fileName, contentType, originalContentType, rubricVersion) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(
            s3Key, _presignedUrlTtl, fileName, inline: true, ct);

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "Rubric URL generated successfully",
            Data = new FileUrlResponse(
                url,
                contentType,
                fileName,
                rubricVersion,
                SubjectFilePreviewService.IsDocx(originalContentType)),
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Download original rubric file (DOCX only).
    /// </summary>
    [HttpGet("{subjectId:guid}/rubric/original")]
    public async Task<IActionResult> GetRubricOriginalUrl(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var info = await _repository.GetRubricOriginalInfoAsync(subjectId, ct);
        if (info is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Original DOCX rubric not available",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, fileName, contentType, rubricVersion) = info.Value;
        var url = await _s3Service.GeneratePresignedGetUrlAsync(
            s3Key, _presignedUrlTtl, fileName, inline: false, ct);

        return Ok(new ApiResponse<FileUrlResponse>
        {
            StatusCode = 200,
            Message = "Original rubric URL generated successfully",
            Data = new FileUrlResponse(url, contentType, fileName, rubricVersion),
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> CreateSubject(
        [FromBody] CreateSubjectRequest request,
        CancellationToken ct = default)
    {
        if (request.MaxScore <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "maxScore must be greater than 0",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (request.PassScore is < 0 || request.PassScore > request.MaxScore)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "passScore must be between 0 and maxScore",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        try
        {
            var subject = await _repository.CreateSubjectAsync(request, ct);
            return CreatedAtAction(nameof(GetSubjectDetail), new { subjectId = subject.Id }, new ApiResponse<SubjectDetailDto>
            {
                StatusCode = 201,
                Message = "Subject created successfully",
                Data = subject,
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Subject code already exists for this exam",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPut("{subjectId:guid}")]
    [AdminOnly]
    public async Task<IActionResult> UpdateSubject(
        Guid subjectId,
        [FromBody] UpdateSubjectRequest request,
        CancellationToken ct = default)
    {
        if (request.MaxScore <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "maxScore must be greater than 0",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (request.PassScore is < 0 || request.PassScore > request.MaxScore)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "passScore must be between 0 and maxScore",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var existing = await _repository.GetSubjectDetailAsync(subjectId, ct);
            if (existing is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Subject not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            if (!Enum.TryParse<SubjectStatus>(request.Status, ignoreCase: true, out var requestedStatus))
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = $"Invalid status value: {request.Status}",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var currentStatus = Enum.Parse<SubjectStatus>(existing.Status, ignoreCase: true);
            var transitionError = _subjectAdminService.ValidateStatusTransition(currentStatus, requestedStatus);
            if (transitionError is not null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = transitionError,
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }
        }

        try
        {
            var subject = await _repository.UpdateSubjectAsync(subjectId, request, ct);
            if (subject is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Subject not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<SubjectDetailDto>
            {
                StatusCode = 200,
                Message = "Subject updated successfully",
                Data = subject,
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Subject code already exists for this exam",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPost("{subjectId:guid}/exam-paper")]
    [AdminOnly]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadExamPaper(
        Guid subjectId,
        IFormFile file,
        CancellationToken ct = default)
    {
        var validationError = ValidateUploadFile(file);
        if (validationError is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = validationError,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var safeFileName = Path.GetFileName(file!.FileName);
        var contentType = NormalizeContentType(file);
        var s3Key = $"exam-papers/{subjectId}/{safeFileName}";

        try
        {
            var fileBytes = await ReadFormFileBytesAsync(file, ct);

            await _s3Service.UploadAsync(
                s3Key, new MemoryStream(fileBytes), contentType, ct);

            var (previewKey, previewContentType) = await _filePreviewService.CreatePreviewAsync(
                subjectId,
                s3Key,
                contentType,
                safeFileName,
                new MemoryStream(fileBytes),
                isRubric: false,
                rubricVersion: 1,
                ct);

            var updated = await _repository.UpdateExamPaperAsync(
                subjectId, s3Key, safeFileName, contentType, previewKey, previewContentType, ct);
            if (!updated)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Subject not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<ExamPaperUploadResponse>
            {
                StatusCode = 200,
                Message = "Exam paper uploaded successfully",
                Data = new ExamPaperUploadResponse(safeFileName, contentType),
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = ex.Message,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPost("{subjectId:guid}/rubric")]
    [AdminOnly]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadRubric(
        Guid subjectId,
        IFormFile file,
        CancellationToken ct = default)
    {
        var validationError = ValidateUploadFile(file);
        if (validationError is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = validationError,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var rubricInfo = await _repository.GetRubricInfoAsync(subjectId, ct);
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var nextVersion = rubricInfo.HasValue ? rubricInfo.Value.RubricVersion + 1 : 1;
        var safeFileName = Path.GetFileName(file!.FileName);
        var contentType = NormalizeContentType(file);
        var s3Key = $"rubrics/{subjectId}/v{nextVersion}/{safeFileName}";

        try
        {
            var fileBytes = await ReadFormFileBytesAsync(file, ct);

            await _s3Service.UploadAsync(
                s3Key, new MemoryStream(fileBytes), contentType, ct);

            var (previewKey, previewContentType) = await _filePreviewService.CreatePreviewAsync(
                subjectId,
                s3Key,
                contentType,
                safeFileName,
                new MemoryStream(fileBytes),
                isRubric: true,
                rubricVersion: nextVersion,
                ct);

            var rubricVersion = await _repository.UpdateRubricAsync(
                subjectId, s3Key, safeFileName, contentType, previewKey, previewContentType, ct);
            if (rubricVersion is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Subject not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<RubricUploadResponse>
            {
                StatusCode = 200,
                Message = "Rubric uploaded successfully",
                Data = new RubricUploadResponse(rubricVersion.Value, safeFileName, contentType),
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = ex.Message,
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPost("{subjectId:guid}/rubric/extract-grid")]
    [AdminOnly]
    public async Task<IActionResult> ExtractRubricGrid(Guid subjectId, CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var rubricInfo = await _repository.GetRubricInfoAsync(subjectId, ct);
        if (rubricInfo is null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Rubric not uploaded for this subject",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (s3Key, _, contentType, _) = rubricInfo.Value;
        var presignedUrl = await _s3Service.GeneratePresignedGetUrlAsync(
            s3Key, TimeSpan.FromMinutes(5), ct: ct);

        var result = await _aiGradingClient.ExtractRubricAsync(
            presignedUrl, contentType, subject.MaxScore, ct);

        if (result is null)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = "AI rubric extraction is unavailable. Enter the score grid manually.",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<ExtractGridResponse>
        {
            StatusCode = 200,
            Message = "Rubric grid extracted successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPut("{subjectId:guid}/questions")]
    [AdminOnly]
    public async Task<IActionResult> ReplaceQuestions(
        Guid subjectId,
        [FromBody] ReplaceQuestionsRequest request,
        CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (errors, warnings) = _subjectAdminService.ValidateQuestions(subject.MaxScore, request.Questions);
        if (errors.Count > 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = string.Join("; ", errors),
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var questions = await _repository.ReplaceQuestionsAsync(subjectId, request.Questions, ct);
        if (questions is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Subject not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<ReplaceQuestionsResponse>
        {
            StatusCode = 200,
            Message = "Questions saved successfully",
            Data = new ReplaceQuestionsResponse(questions, warnings),
            ResponsedAt = DateTime.UtcNow
        });
    }

    private static string NormalizeContentType(IFormFile file)
    {
        if (AllowedContentTypes.Contains(file.ContentType))
        {
            return file.ContentType;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            _ => file.ContentType
        };
    }

    private static string? ValidateUploadFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "File is required and must not be empty";
        }

        if (file.Length > MaxUploadBytes)
        {
            return "File exceeds maximum allowed size (50 MB)";
        }

        var contentType = NormalizeContentType(file);
        if (!AllowedContentTypes.Contains(contentType))
        {
            return "Unsupported file type. Allowed: PDF, DOCX, TXT, JPEG, PNG, WEBP";
        }

        return null;
    }

    private static async Task<byte[]> ReadFormFileBytesAsync(IFormFile file, CancellationToken ct)
    {
        await using var uploadStream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await uploadStream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private bool IsAdmin()
    {
        // TokenService issues the role claim with literal Type "Role" (not the ClaimTypes.Role URI,
        // and not lowercase "role") — .NET's default inbound claim map only remaps "role" (lowercase),
        // so neither ClaimTypes.Role nor "role" ever matches a real token. Must match the exact case.
        var role = User.FindFirst("Role")?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
