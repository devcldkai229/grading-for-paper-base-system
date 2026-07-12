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
}
