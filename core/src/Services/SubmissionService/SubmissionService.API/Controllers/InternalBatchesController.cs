using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.API.Controllers;

/// <summary>
/// Internal endpoints for GradingService (ApiKey — not exposed via gateway).
/// </summary>
[Route("api/internal/batches")]
[ApiController]
public class InternalBatchesController : ControllerBase
{
    private readonly IStudentPaperRepository _paperRepository;

    public InternalBatchesController(IStudentPaperRepository paperRepository)
    {
        _paperRepository = paperRepository;
    }

    [HttpGet("{batchId:guid}/papers")]
    public async Task<IActionResult> GetBatchPapers(Guid batchId, CancellationToken ct = default)
    {
        var result = await _paperRepository.GetPapersByBatchAsync(batchId, ct);
        if (result is null)
        {
            return NotFound(new { statusCode = 404, message = "Batch not found", responsedAt = DateTime.UtcNow });
        }

        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }
}
