using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.API.Controllers;

[Route("api/internal/papers")]
[ApiController]
public class InternalPapersController : ControllerBase
{
    private readonly IStudentPaperRepository _paperRepository;

    public InternalPapersController(IStudentPaperRepository paperRepository)
    {
        _paperRepository = paperRepository;
    }

    [HttpGet("{paperId:guid}")]
    public async Task<IActionResult> GetPaperSummary(Guid paperId, CancellationToken ct = default)
    {
        var summary = await _paperRepository.GetPaperSummaryAsync(paperId, ct);
        if (summary is null)
        {
            return NotFound(new { statusCode = 404, message = "Paper not found", responsedAt = DateTime.UtcNow });
        }

        return Ok(new { data = summary, responsedAt = DateTime.UtcNow });
    }
}
