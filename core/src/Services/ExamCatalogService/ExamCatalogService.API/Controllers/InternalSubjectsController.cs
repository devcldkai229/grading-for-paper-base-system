using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ExamCatalogService.API.Controllers;

[Route("api/internal/subjects")]
[ApiController]
public class InternalSubjectsController : ControllerBase
{
    private readonly IExamCatalogRepository _repository;

    public InternalSubjectsController(IExamCatalogRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{subjectId:guid}/grading-grid")]
    public async Task<IActionResult> GetGradingGrid(Guid subjectId, CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new { statusCode = 404, message = "Subject not found", responsedAt = DateTime.UtcNow });
        }

        var grid = new
        {
            subjectId = subject.Id,
            status = subject.Status,
            maxScore = subject.MaxScore,
            rubricVersion = subject.RubricVersion,
            questions = subject.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new
                {
                    questionNumber = q.QuestionNumber,
                    groupLabel = q.GroupLabel,
                    label = q.Label,
                    maxScore = q.MaxScore,
                    orderIndex = q.OrderIndex
                })
        };

        return Ok(new { data = grid, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Plain-text rubric summary for AI grading (question labels + max scores).
    /// </summary>
    [HttpGet("{subjectId:guid}/rubric-text")]
    public async Task<IActionResult> GetRubricText(Guid subjectId, CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new { statusCode = 404, message = "Subject not found", responsedAt = DateTime.UtcNow });
        }

        var lines = subject.Questions
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.QuestionNumber)
            .Select(q =>
            {
                var group = string.IsNullOrWhiteSpace(q.GroupLabel) ? "" : $" [{q.GroupLabel}]";
                var label = string.IsNullOrWhiteSpace(q.Label) ? "" : $" — {q.Label}";
                return $"{q.QuestionNumber}{group}{label} (max {q.MaxScore})";
            });

        var text = string.Join('\n', lines);
        return Ok(new { data = new { text }, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Minimal exam context for a subject (code, exam name, exam end date).
    /// Consumed by GradingService to surface upcoming grading deadlines on a lecturer's dashboard.
    /// </summary>
    [HttpGet("{subjectId:guid}/exam-info")]
    public async Task<IActionResult> GetExamInfo(Guid subjectId, CancellationToken ct = default)
    {
        var info = await _repository.GetSubjectExamInfoAsync(subjectId, ct);
        if (info is null)
        {
            return NotFound(new { statusCode = 404, message = "Subject not found", responsedAt = DateTime.UtcNow });
        }

        return Ok(new { data = info, responsedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Lists every subject matching an optional semester/exam filter (unpaginated — bounded by how
    /// many subjects an exam catalog realistically has). Consumed by ReportingService to resolve
    /// which subjects fall under a semester/exam filter for the admin grading progress dashboard.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchSubjects(
        [FromQuery] Guid? semesterId, [FromQuery] Guid? examId, CancellationToken ct = default)
    {
        var (items, _) = await _repository.SearchSubjectsAsync(
            code: null, semesterId, examId, status: null, restrictToSubjectIds: null,
            page: 1, pageSize: 1000, ct);

        return Ok(new { data = items, responsedAt = DateTime.UtcNow });
    }
}
