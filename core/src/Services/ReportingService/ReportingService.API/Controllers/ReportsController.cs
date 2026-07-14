using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using System.Security.Claims;

namespace ReportingService.API.Controllers;

[Route("api/reports")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private const long MaxUploadBytes = 5_242_880; // 5 MB — mapping files are small (roster lists)

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".xlsx"
    };

    private readonly IFeedbackReportService _feedbackReportService;
    private readonly IGradingProgressService _gradingProgressService;
    private readonly IScoreDistributionService _scoreDistributionService;
    private readonly IPassFailReportService _passFailReportService;

    public ReportsController(
        IFeedbackReportService feedbackReportService,
        IGradingProgressService gradingProgressService,
        IScoreDistributionService scoreDistributionService,
        IPassFailReportService passFailReportService)
    {
        _feedbackReportService = feedbackReportService;
        _gradingProgressService = gradingProgressService;
        _scoreDistributionService = scoreDistributionService;
        _passFailReportService = passFailReportService;
    }

    /// <summary>
    /// Admin-only. Pass/fail report: student counts and pass rate per subject, computed against
    /// each subject's configured PassScore threshold. Optionally scoped to a semester and/or exam.
    /// </summary>
    [HttpGet("pass-fail")]
    public async Task<IActionResult> GetPassFailReport(
        [FromQuery] Guid? semesterId,
        [FromQuery] Guid? examId,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var (result, error) = await _passFailReportService.GetReportAsync(semesterId, examId, ct);
        if (error is not null)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<PassFailReportResultDto>
        {
            StatusCode = 200,
            Message = "Pass/fail report retrieved successfully",
            Data = result!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Admin-only. Score distribution report: a 10-bucket histogram plus min/avg/max per subject.
    /// Optionally scoped to a semester and/or exam.
    /// </summary>
    [HttpGet("score-distribution")]
    public async Task<IActionResult> GetScoreDistribution(
        [FromQuery] Guid? semesterId,
        [FromQuery] Guid? examId,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var (result, error) = await _scoreDistributionService.GetDistributionAsync(semesterId, examId, ct);
        if (error is not null)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<ScoreDistributionResultDto>
        {
            StatusCode = 200,
            Message = "Score distribution report retrieved successfully",
            Data = result!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Admin-only. Grading progress dashboard: completion %, throughput and ETA per subject and
    /// per lecturer. Optionally scoped to a semester and/or exam.
    /// </summary>
    [HttpGet("grading-progress")]
    public async Task<IActionResult> GetGradingProgress(
        [FromQuery] Guid? semesterId,
        [FromQuery] Guid? examId,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var (result, error) = await _gradingProgressService.GetDashboardAsync(semesterId, examId, ct);
        if (error is not null)
        {
            return StatusCode(503, new ApiResponse<object>
            {
                StatusCode = 503,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<GradingProgressDashboardResultDto>
        {
            StatusCode = 200,
            Message = "Grading progress dashboard retrieved successfully",
            Data = result!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Admin-only. Exports one row per student with releasable feedback (score, paper comment,
    /// per-question comments — never internal notes) for every Submitted paper in a subject,
    /// de-anonymised via an uploaded alias↔student mapping file (columns: AliasNumber, StudentCode, StudentName).
    /// </summary>
    [HttpPost("subjects/{subjectId:guid}/feedback-export")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> ExportFeedback(
        Guid subjectId,
        IFormFile aliasMapping,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (aliasMapping is null || aliasMapping.Length == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Alias mapping file (AliasNumber, StudentCode, StudentName) is required.",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var ext = Path.GetExtension(aliasMapping.FileName);
        if (!AllowedExtensions.Contains(ext))
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Unsupported mapping file type. Allowed: .csv, .xlsx",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var requestedBy = GetUserId();

        await using var stream = aliasMapping.OpenReadStream();
        var (bytes, fileName, error) = await _feedbackReportService.GenerateFeedbackReportAsync(
            subjectId, requestedBy, stream, aliasMapping.FileName, ct);

        if (error is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return File(
            bytes!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
        return Guid.Parse(raw);
    }

    private bool IsAdmin()
    {
        // TokenService issues the role claim with literal Type "Role" (not the ClaimTypes.Role URI,
        // and not lowercase "role") — see other services' controllers for the same fix/rationale.
        var role = User.FindFirst("Role")?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
