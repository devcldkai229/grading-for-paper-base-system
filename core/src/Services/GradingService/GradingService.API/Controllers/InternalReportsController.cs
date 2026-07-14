using GradingService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingService.API.Controllers;

/// <summary>
/// Internal (service-to-service) endpoints for ReportingService.
/// Protected by InternalApiKeyMiddleware (path prefix /api/internal) — no JWT/[Authorize] here.
/// </summary>
[ApiController]
[Route("api/internal/subjects")]
public class InternalReportsController : ControllerBase
{
    private readonly IGradingSessionService _gradingSessionService;

    public InternalReportsController(IGradingSessionService gradingSessionService)
    {
        _gradingSessionService = gradingSessionService;
    }

    /// <summary>
    /// Releasable (public-facing) feedback for every Submitted paper in a subject.
    /// Never includes GradingForm.InternalComment.
    /// </summary>
    [HttpGet("{subjectId:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid subjectId, CancellationToken ct = default)
    {
        var result = await _gradingSessionService.GetReleasableFeedbackAsync(subjectId, ct);
        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Admin grading progress dashboard (completion %, throughput, ETA) per subject and per lecturer.
    /// Pass one or more <paramref name="subjectIds"/> to scope the result (e.g. resolved from a
    /// semester/exam filter); omit to cover every subject with at least one grading assignment.
    /// </summary>
    [HttpGet("~/api/internal/grading/progress")]
    public async Task<IActionResult> GetProgressDashboard([FromQuery] Guid[]? subjectIds, CancellationToken ct = default)
    {
        var result = await _gradingSessionService.GetProgressDashboardAsync(subjectIds, ct);
        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Raw submitted scores + min/avg/max per subject, for the admin score distribution report.
    /// Pass one or more <paramref name="subjectIds"/> to scope the result (e.g. resolved from a
    /// semester/exam filter); omit to cover every subject with at least one submitted paper.
    /// </summary>
    [HttpGet("~/api/internal/grading/score-distribution")]
    public async Task<IActionResult> GetScoreDistribution([FromQuery] Guid[]? subjectIds, CancellationToken ct = default)
    {
        var result = await _gradingSessionService.GetScoreDistributionAsync(subjectIds, ct);
        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Unscoped, filterable audit log listing for ReportingService's global audit log viewer.
    /// </summary>
    [HttpGet("~/api/internal/grading/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 50;

        var result = await _gradingSessionService.GetAuditLogsAsync(userId, entityType, action, page, pageSize, ct);
        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }
}
