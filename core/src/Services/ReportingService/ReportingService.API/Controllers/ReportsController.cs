using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ReportsController(IFeedbackReportService feedbackReportService)
    {
        _feedbackReportService = feedbackReportService;
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
