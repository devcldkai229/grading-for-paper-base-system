using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.API.Controllers;

[Route("api/internal/papers")]
[ApiController]
public class InternalPapersController : ControllerBase
{
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly PresignedUrlOptions _presignedUrlOptions;

    public InternalPapersController(
        IStudentPaperRepository paperRepository,
        IS3Service s3Service,
        PresignedUrlOptions presignedUrlOptions)
    {
        _paperRepository = paperRepository;
        _s3Service = s3Service;
        _presignedUrlOptions = presignedUrlOptions;
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

    /// <summary>
    /// Aggregate paper stats (total count + highest alias number) for a subject. Consumed by
    /// GradingService to validate marker-assignment alias ranges against what was actually submitted.
    /// </summary>
    [HttpGet("subjects/{subjectId:guid}/stats")]
    public async Task<IActionResult> GetSubjectPaperStats(Guid subjectId, CancellationToken ct = default)
    {
        var stats = await _paperRepository.GetSubjectPaperStatsAsync(subjectId, ct);
        return Ok(new { data = stats, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Pre-signed GET URLs for every file on a paper. Consumed by GradingService → AIGradingService.
    /// </summary>
    [HttpGet("{paperId:guid}/files")]
    public async Task<IActionResult> GetPaperFiles(Guid paperId, CancellationToken ct = default)
    {
        var detail = await _paperRepository.GetPaperDetailAsync(paperId, ct);
        if (detail is null)
        {
            return NotFound(new { statusCode = 404, message = "Paper not found", responsedAt = DateTime.UtcNow });
        }

        var files = new List<object>();
        foreach (var file in detail.Files)
        {
            var info = await _paperRepository.GetPaperFileInfoAsync(paperId, file.Id, ct);
            if (info is null) continue;

            var (s3Key, fileName, contentType) = info.Value;
            var url = await _s3Service.GeneratePresignedGetUrlAsync(s3Key, _presignedUrlOptions.Ttl, ct);
            files.Add(new { url, contentType, fileName });
        }

        return Ok(new { data = files, responsedAt = DateTime.UtcNow });
    /// Bulk-resolves summaries (alias, batch, subject) for an arbitrary set of paper ids in one
    /// round trip. Consumed by GradingService to attach aliases to a lecturer's grading queue.
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> GetPapersByIds(
        [FromBody] BatchPaperIdsRequest request, CancellationToken ct = default)
    {
        var summaries = await _paperRepository.GetPapersByIdsAsync(request.PaperIds, ct);
        return Ok(new { data = summaries, responsedAt = DateTime.UtcNow });
    }
}

public record BatchPaperIdsRequest(IReadOnlyList<Guid> PaperIds);
