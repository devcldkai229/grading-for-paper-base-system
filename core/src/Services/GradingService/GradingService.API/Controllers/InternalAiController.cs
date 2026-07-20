using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingService.API.Controllers;

/// <summary>
/// Internal (service-to-service) endpoints for AIGradingService write-back.
/// Protected by InternalApiKeyMiddleware (path prefix /api/internal) — no JWT/[Authorize] here.
/// </summary>
[ApiController]
[Route("api/internal/ai")]
public class InternalAiController : ControllerBase
{
    private readonly IGradingSessionService _gradingSessionService;

    public InternalAiController(IGradingSessionService gradingSessionService)
    {
        _gradingSessionService = gradingSessionService;
    }

    /// <summary>
    /// Applies AI-generated score suggestions to a grading assignment.
    /// Called by AIGradingService after POST /ai/grade/paper completes.
    /// </summary>
    [HttpPost("ai-suggestions")]
    public async Task<IActionResult> ApplyAiSuggestions(
        [FromBody] ApplyAiSuggestionsRequest request,
        CancellationToken ct = default)
    {
        var (success, error) = await _gradingSessionService.ApplyAiSuggestionsAsync(request, ct);

        if (!success)
        {
            var status = string.Equals(error, "Assignment not found", StringComparison.OrdinalIgnoreCase)
                ? 404
                : 400;

            return StatusCode(status, new
            {
                statusCode = status,
                message = error ?? "Failed to apply AI suggestions",
                responsedAt = DateTime.UtcNow
            });
        }

        return Ok(new { data = new { applied = true }, responsedAt = DateTime.UtcNow });
    }
}
